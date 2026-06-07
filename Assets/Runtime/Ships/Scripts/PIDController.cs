public class PIDController
{
    private float kP;
    private float kI;
    private float kD;

    private float integral;
    private float previousError;

    public PIDController(float p, float i, float d)
    {
        kP = p;
        kI = i;
        kD = d;
    }

    public float GetOutput(float error, float deltaTime)
    {
        if (deltaTime <= 0f)
            return 0f;

        integral += error * deltaTime;

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