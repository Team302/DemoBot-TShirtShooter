using System;
using System.Threading;
using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;

namespace TShirtCannon
{
    public class Program
    {
        // ROBOT MAPPING 
        public const int RIGHT_MASTER_TALON = 1;
        public const int RIGHT_SLAVE_TALON = 2;
        public const int LEFT_MASTER_TALON = 3;
        public const int LEFT_SLAVE_TALON = 4;
        public const int SOLENOID_CONTROLLER = 0;
        // BUTTON MAPPING
        public const int A_BUTTON = 2;
        public const int B_BUTTON = 3;
        public const int X_BUTTON = 1;
        public const int Y_BUTTON = 4;
        public const int RIGHT_BUMPER = 6;
        public const int LEFT_BUMPER = 5;

        // AXIS MAPPING
        public const int RIGHT_TRIGGER = CTRE.Phoenix.Controller.LogitechGamepad.kAxis_RightShoulder;
        public const int LEFT_TRIGGER = CTRE.Phoenix.Controller.LogitechGamepad.kAxis_LeftShoulder;
        public const int RIGHT_JOYSTICK_X = CTRE.Phoenix.Controller.LogitechGamepad.kAxis_RightX;
        public const int RIGHT_JOYSTICK_Y = CTRE.Phoenix.Controller.LogitechGamepad.kAxis_RightY;
        public const int LEFT_JOYSTICK_X = CTRE.Phoenix.Controller.LogitechGamepad.kAxis_LeftX;
        public const int LEFT_JOYSTICK_Y = CTRE.Phoenix.Controller.LogitechGamepad.kAxis_LeftY;

        // Constants
        public const double LOOP_LENGTH = 0.02;
        public const double SHOOT_TIME = 0.2;

        // Variables
        public static int currentBarrel = 0;
        public static bool prevShootStateMain = false;
        public static bool prevShootStateOne = false;
        public static bool prevShootStateTwo = false;

        public static double[] shootTimers = new double[6];

        public static bool reloading = false;
        public static bool prevReloadState = false;

        // Creating the controller
        static CTRE.Phoenix.Controller.GameController m_controller = new CTRE.Phoenix.Controller.GameController(new CTRE.Phoenix.UsbHostDevice(0));

        // Creating drive talons
        static CTRE.Phoenix.MotorControl.CAN.TalonSRX m_masterRight = new CTRE.Phoenix.MotorControl.CAN.TalonSRX(RIGHT_MASTER_TALON);
        static CTRE.Phoenix.MotorControl.CAN.TalonSRX m_slaveRight = new CTRE.Phoenix.MotorControl.CAN.TalonSRX(RIGHT_SLAVE_TALON);
        static CTRE.Phoenix.MotorControl.CAN.TalonSRX m_masterLeft = new CTRE.Phoenix.MotorControl.CAN.TalonSRX(LEFT_MASTER_TALON);
        static CTRE.Phoenix.MotorControl.CAN.TalonSRX m_slaveLeft = new CTRE.Phoenix.MotorControl.CAN.TalonSRX(LEFT_SLAVE_TALON);

        // Create Pnuematic Cannons
        static CTRE.Phoenix.PneumaticControlModule m_cannon = new CTRE.Phoenix.PneumaticControlModule(SOLENOID_CONTROLLER);

        public static void Main()
        {
            // Loop for running Tshirt Code
            while (true)
            {
                // CTRE Saftey feature to make it so that if the controller is disconnected we no longer send values to the motor controllers
                if (m_controller.GetConnectionStatus() == CTRE.Phoenix.UsbDeviceConnection.Connected)
                    CTRE.Phoenix.Watchdog.Feed(); // Allows motor control

                ArcadeDrive();
                CannonShoot();

                System.Threading.Thread.Sleep(20);
            }
        }

        // Allows the gamepad to be a little less touchy
        static double Deadband(float value)
        {
            if (value < -0.075)
            {
                /* outside of deadband */
                return value;
            }
            else if (value > +0.075)
            {
                /* outside of deadband */
                return value;
            }
            else
            {
                /* within 10% so zero it */
                return value = 0;
            }
        }

        // Arcade Drive Controller
        static void ArcadeDrive()
        {
            // Set slaves and inversion
            m_masterRight.SetInverted(true);
            m_slaveRight.SetInverted(true);
            m_slaveRight.Follow(m_masterRight);
            m_masterLeft.SetInverted(false);
            m_slaveLeft.SetInverted(false);
            m_slaveLeft.Follow(m_masterLeft);

            // Get Axis Value (-1.0 to 1.0)
            float y = m_controller.GetAxis(LEFT_JOYSTICK_Y);
            float x = m_controller.GetAxis(RIGHT_JOYSTICK_X);

            // Set speed values
            double throttle = Deadband(y); // Throttle equals y axis value of left joystick with deadband
            double steer = Deadband(x); // Steer equals x axis value of right joystick with deadband

            throttle = System.Math.Pow(throttle, 3.0);
            steer = System.Math.Pow(steer, 3.0);

            double rightSpeed = throttle + steer;
            double leftSpeed = throttle - steer;

            // If either right or left speed are out of range (-1.0 to 1.0) Scale both until in range
            double maxValue = 0;
            if (System.Math.Abs(leftSpeed) > maxValue)
                maxValue = System.Math.Abs(leftSpeed);
            if (System.Math.Abs(rightSpeed) > maxValue)
                maxValue = System.Math.Abs(rightSpeed);
            //Scale down all values if max > 1.0
            if (maxValue > 1.0)
            {
                leftSpeed /= maxValue;
                rightSpeed /= maxValue;
            }

            // Set outputs
            m_masterRight.Set(CTRE.Phoenix.MotorControl.ControlMode.PercentOutput, rightSpeed);
            m_masterLeft.Set(CTRE.Phoenix.MotorControl.ControlMode.PercentOutput, leftSpeed);
        }

        //shoots cannons each time shoot button is pressed in ascending order
        static void CannonShoot()
        {
            // bool flip so that you dont hold down button and rapid fire
            bool currentShootStateMain = m_controller.GetButton(A_BUTTON);  // A button for as trigger 
            bool currentShootStateOne = m_controller.GetButton(X_BUTTON);   // Right Bumper + A button for shooting one cannon 
            bool currentShootStateTwo = m_controller.GetButton(B_BUTTON);    // Left Bumper + A button for shooting six cannons

            if (!prevShootStateMain && currentShootStateMain)
            {
                // starts timer
                shootTimers[currentBarrel] = SHOOT_TIME;
                // flips through barrels
                currentBarrel++;
                if (currentBarrel > 5)
                    currentBarrel = 0;
                Debug.Print(currentBarrel.ToString());
            }

            // secret shoot all 6 at same time command
            if (!prevShootStateTwo && currentShootStateTwo)
            {
                // starts all timers while flipping through barrels
                for (int i = 0; i < shootTimers.Length; i++)
                {
                    shootTimers[currentBarrel] = SHOOT_TIME;
                    currentBarrel++;
                    if (currentBarrel > 5)
                        currentBarrel = 0;

                    Debug.Print(currentBarrel.ToString());
                }
                Debug.Print("Shoot Six");
            }
            else if (prevShootStateTwo && !currentShootStateTwo)
                Debug.Print("End Shoot Six");

            prevShootStateOne = currentShootStateOne;
            prevShootStateTwo = currentShootStateTwo;
            prevShootStateMain = currentShootStateMain;

            Shoot();
            ReduceTimers();
        }

        private static void Shoot()
        {
            // shoots all that should be shot and stops all that should be stopped
            for (int i = 0; i < shootTimers.Length; i++)
            {
                m_cannon.SetSolenoidOutput(i, shootTimers[i] > 0);
            }
        }

        private static void ReduceTimers()
        {
            // reduces timers lololol
            for (int i = 0; i < shootTimers.Length; i++)
            {
                shootTimers[i] -= LOOP_LENGTH;
            }
        }
    }
}