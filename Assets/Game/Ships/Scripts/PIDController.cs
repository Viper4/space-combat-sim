using UnityEngine;

public class PIDController
{
    float kP;
    float kI;
    float kD;

    float integral;
    float previousError;

    private float maxIntegral;

    public PIDController(float p, float i, float d, float maxIntegral)
    {
        kP = p;
        kI = i;
        kD = d;
        this.maxIntegral = maxIntegral;
    }

    public float GetOutput(float error, float deltaTime)
    {
        if (deltaTime <= 0f)
            return 0f;

        integral += error * deltaTime;

        //integral = Mathf.Clamp(integral, -maxIntegral, maxIntegral);

        float derivative = (error - previousError) / deltaTime;

        previousError = error;

        return kP * error + kI * integral + kD * derivative;
    }

    public void Reset()
    {
        integral = 0;
        previousError = 0;
    }
}