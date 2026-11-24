using UnityEngine;
using System.Collections;

public class ScreenManager : MonoBehaviour
{
    [Header("Screen Settings")]
    [SerializeField] private ScreenBounds currentScreen;

    [Header("Transition Settings")]
    [SerializeField] private CameraFollow cameraFollow;
    [SerializeField] private Rigidbody2D playerBody;

    [SerializeField] private MonoBehaviour[] playerControls;
    [SerializeField] private float transitionDuration = 0.5f;

    [Header("Travel Zone Settings")]
    [SerializeField] private float clearanceDistance = 0.05f;

    private ScreenTravelZone blockedZone;
    private Collider2D blockedCollider;
    private Bounds blockedBounds;

    private int physicsStepsRemaining;

    public ScreenBounds CurrentScreen => currentScreen;
    public bool IsTransitioning { get; private set; }

    public void SetCurrentScreen(ScreenBounds newScreen)
    {
        currentScreen = newScreen;
    }

    public void TryTravel(ScreenTravelZone sourceZone, Rigidbody2D enteringBody)
    {
        if (enteringBody != playerBody)
            return;

        if (IsTransitioning)
            return;

        if (sourceZone == blockedZone)
            return;

        if (sourceZone.OwnerScreen != CurrentScreen)
            return;

        ScreenTravelZone destination = sourceZone.LinkedZone;

        if (destination == null)
        {
            Debug.LogWarning("Travel zone has no connection.");
            return;
        }

        if (destination.OwnerScreen == null || destination.ArrivalPoint == null)
        {
            Debug.LogWarning("Destination is not configured.");
            return;
        }

        if (destination.OwnerScreen == CurrentScreen)
            return;

        if (sourceZone.OutwardDirection != -destination.OutwardDirection)
        {
            Debug.LogWarning("Travel zone directions do not match.");
            return;
        }

        Collider2D destinationCollider = destination.GetComponent<Collider2D>();

        if (destinationCollider == null || !destinationCollider.isTrigger || !destinationCollider.enabled)
        {
            Debug.LogWarning("Destination trigger is not configured.");
            return;
        }

        Debug.Log("Travelling from " + sourceZone.OwnerScreen.name + " to " + destination.OwnerScreen.name);

        StartCoroutine(TravelRoutine(sourceZone));
    }

    private void BlockZone(ScreenTravelZone zone)
    {
        ReleaseBlockedZone();

        blockedZone = zone;
        blockedCollider = zone.GetComponent<Collider2D>();

        // Store the bounds before disabling the collider.
        blockedBounds = blockedCollider.bounds;

        blockedCollider.enabled = false;

        Debug.Log("Blocked travel zone: " + zone.name);
    }

    private void ReleaseBlockedZone()
    {
        if (blockedCollider != null)
        {
            blockedCollider.enabled = true;

            Debug.Log("Unlocked travel zone: " + blockedZone.name);
        }

        blockedZone = null;
        blockedCollider = null;
    }

    private void CheckZoneClearance()
    {
        // Direction pointing into the destination screen.
        Vector2 inwardDirection = -blockedZone.OutwardDirection;

        Collider2D[] playerColliders = playerBody.GetComponentsInChildren<Collider2D>();

        // Synchronise collider positions with their transforms.
        Physics2D.SyncTransforms();

        bool foundCollider = false;

        foreach (Collider2D playerCollider in playerColliders)
        {
            if (!playerCollider.enabled)
                continue;

            if (playerCollider.attachedRigidbody != playerBody)
                continue;

            foundCollider = true;

            Bounds playerBounds = playerCollider.bounds;

            // Player arrived from the West.
            if (inwardDirection == Vector2.right)
            {
                if (playerBounds.min.x < blockedBounds.max.x + clearanceDistance)
                    return;
            }

            // Player arrived from the East.
            else if (inwardDirection == Vector2.left)
            {
                if (playerBounds.max.x > blockedBounds.min.x - clearanceDistance)
                    return;
            }

            // Player arrived from the South.
            else if (inwardDirection == Vector2.up)
            {
                if (playerBounds.min.y < blockedBounds.max.y + clearanceDistance)
                    return;
            }

            // Player arrived from the North.
            else if (inwardDirection == Vector2.down)
            {
                if (playerBounds.max.y > blockedBounds.min.y - clearanceDistance)
                    return;
            }
        }

        // Every player collider has cleared the passage.
        if (foundCollider)
        {
            ReleaseBlockedZone();
        }
    }

    private void FixedUpdate()
    {
        if (IsTransitioning || blockedZone == null)
            return;

        // Allow physics to settle after the transition.
        if (physicsStepsRemaining > 0)
        {
            physicsStepsRemaining--;
            return;
        }

        CheckZoneClearance();
    }

    private IEnumerator TravelRoutine(ScreenTravelZone sourceZone)
    {
        IsTransitioning = true;

        ScreenTravelZone destinationZone = sourceZone.LinkedZone;

        // Disable the destination trigger immediately.
        BlockZone(destinationZone);

        // Store player momentum.
        Vector2 storedVelocity = playerBody.linearVelocity;
        float storedAngularVelocity = playerBody.angularVelocity;

        // Store and disable player controls.
        bool[] controlStates = new bool[playerControls.Length];

        for (int i = 0; i < playerControls.Length; i++)
        {
            if (playerControls[i] == null)
                continue;

            controlStates[i] = playerControls[i].enabled;
            playerControls[i].enabled = false;
        }

        // Suspend player physics.
        playerBody.simulated = false;

        // Calculate camera positions.
        Vector2 startPosition = cameraFollow.transform.position;
        Vector2 arrivalPosition = destinationZone.ArrivalPoint.position;

        Vector2 endPosition = cameraFollow.ClampToScreen(arrivalPosition, destinationZone.OwnerScreen);

        float elapsedTime = 0f;

        // Transition the camera.
        while (elapsedTime < transitionDuration)
        {
            elapsedTime += Time.deltaTime;

            float progress = elapsedTime / transitionDuration;
            progress = Mathf.SmoothStep(0f, 1f, progress);

            Vector2 cameraPosition = Vector2.Lerp(startPosition, endPosition, progress);

            cameraFollow.SetCameraPosition(cameraPosition);

            yield return null;
        }

        cameraFollow.SetCameraPosition(endPosition);

        // Move the player into the destination screen.
        playerBody.position = arrivalPosition;

        SetCurrentScreen(destinationZone.OwnerScreen);

        // Restore player physics.
        playerBody.simulated = true;

        Physics2D.SyncTransforms();

        // Restore player controls.
        for (int i = 0; i < playerControls.Length; i++)
        {
            if (playerControls[i] == null)
                continue;

            playerControls[i].enabled = controlStates[i];
        }

        // Restore player momentum.
        playerBody.linearVelocity = storedVelocity;
        playerBody.angularVelocity = storedAngularVelocity;

        // Keep the destination blocked through initial physics updates.
        physicsStepsRemaining = 2;

        IsTransitioning = false;
    }
}
