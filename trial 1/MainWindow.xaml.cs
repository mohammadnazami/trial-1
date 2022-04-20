using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO.Ports;
using System.Windows.Threading;
using System.Drawing;
using System.IO;
using AForge.Video;
using AForge.Video.DirectShow;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Threading;
using AForge.Imaging;


namespace trial_1
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        bool btnstart;
        public DispatcherTimer timer1;
        public bool BlnConnect;                                 //Connection Status
        public static SerialPort SCPort = null;                 //Define serial port
        public string StrReceiver;                              //Receive the string from controller
        private bool BlnBusy;                                   //If controller is busy
        public bool BlnReadCom;                                 //If reading is finished, return TRUE
        public bool BlnStopCommand;                             //Stop waiting
        public short ShrPort;                                   //The serial port number
        private bool BlnSet;                                    //If the command sent is a set command or an inquiry command. TRUE is a set command
        private double DblPulseEqui;                            //Pulse equivalent
        short sSpeed;                                           //Current speed
        long lCurrStep;                                         //Current steps
        double dCurrPosi;                                       //Current position
        public long x_save_position;
        public long x_touch_position;
        public string messageboxtext;
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {

        }

        private void connect_Click(object sender, RoutedEventArgs e)
        {
            try
            {


                ConnectPort(Convert.ToInt16(portnumber.Text));

                //recievend position right after connecting
                inquire_current_position_X();
                inquire_current_position_Y();
                inquire_current_position_Z();
                BlnConnect = true;
            }
            catch(Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
        public void ConnectPort(short sPort)
        {
           if (SCPort.IsOpen == true) SCPort.Close();
            SCPort.PortName = "COM" + sPort.ToString();            //Set the serial port number
            SCPort.BaudRate = 9600;                                //Set the bit rate
            SCPort.DataBits = 8;                                   //Set the data bits
            SCPort.StopBits = StopBits.One;                        //Set the stop bit
            SCPort.Parity = Parity.None;                           //Set the Parity
            SCPort.ReadBufferSize = 2048;
            SCPort.WriteBufferSize = 1024;
            SCPort.DtrEnable = true;
            SCPort.Handshake = Handshake.None;
            SCPort.ReceivedBytesThreshold = 1;
            SCPort.RtsEnable = false;

            //This delegate should be a trigger event for fetching data asynchronously, it will be triggered when there is data passed from serial port.
            SCPort.DataReceived += new SerialDataReceivedEventHandler(SCPort_DataReceived);     //DataReceivedEvent delegate
            try
            {
                SCPort.Open();                                     //Open serial port
                if (SCPort.IsOpen)
                {
                    StrReceiver = "";
                    BlnBusy = true;
                    BlnSet = false;
                    SendCommand("?R\r");                           //Connect to the controller
                    Delay(10000);
                    BlnBusy = false;

                    if (StrReceiver == "?R\rOK\n")
                    {
                        BlnConnect = true;                        //Connected successfully
                        ShrPort = sPort;                          //Setial port number
                        connection_textbox.Text = "Connected \n Successfully";
                    }
                    else
                    {
                        BlnBusy = false;
                        BlnConnect = false;
                        connection_textbox.Text = "Failed to connect";
                      //  MessageBox.Show("Failed to connect", "Information");
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }


        public void SCPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            //****************************************************************
            //Function: SCPort_DataReceived
            //Parameters: 
            //Description: receive the data sent from serial port and handle
            //Return:
            //****************************************************************
            try
            {
                string sCurString = "";
                //Loop to receive the data from serial port
                System.Threading.Thread.Sleep(100);
                sCurString = SCPort.ReadExisting();
                if (sCurString != "")
                    StrReceiver = sCurString;
                if (BlnSet == true)
                {
                    if (StrReceiver.Length == 3)
                    {
                        if (StrReceiver.Substring(StrReceiver.Length - 3) == "OK\n")
                            BlnReadCom = true;
                    }
                    else if (StrReceiver.Length == 4)
                    {
                        if (StrReceiver.Substring(StrReceiver.Length - 3) == "OK\n" || StrReceiver.Substring(StrReceiver.Length - 4) == "OK\nS")
                            BlnReadCom = true;
                    }
                    else
                    {
                        if (StrReceiver.Substring(StrReceiver.Length - 3) == "OK\n" || StrReceiver.Substring(StrReceiver.Length - 4) == "OK\nS" ||
                            StrReceiver.Substring(StrReceiver.Length - 5) == "ERR1\n" || StrReceiver.Substring(StrReceiver.Length - 5) == "ERR3\n" ||
                            StrReceiver.Substring(StrReceiver.Length - 5) == "ERR4\n" || StrReceiver.Substring(StrReceiver.Length - 5) == "ERR5\n")
                            BlnReadCom = true;
                    }
                }
                else
                {
                    if (StrReceiver.Substring(StrReceiver.Length - 1, 1) == "\n")
                        BlnReadCom = true;
                }
            }
            catch (Exception ex)
            {
             //   MessageBox.Show("Failed to receive data", "Information");
            }
        }

        public void SendCommand(string CommandString)
        {
            //****************************************************************
            //Function: SendCommand
            //Parameters: CommandString: the command string
            //Description: send the command to controller
            //Return:
            //****************************************************************
            if (SCPort.IsOpen)
            {
                SCPort.Write(CommandString);
                SCPort.DiscardOutBuffer();
            }
        }


        public void Delay(long milliSecond = 500)
        {
            //****************************************************************
            //Function: Delay
            //Parameters: milliSecond:the waiting time, unit is millsecond
            //Description: appoint the waiting time and exit waiting until the data reading is finished or clicking the stop button or close the window or the waiting time is over.
            //Return:
            //****************************************************************
            int start = Environment.TickCount;

            BlnReadCom = false;
            BlnStopCommand = false;
            while (Math.Abs(Environment.TickCount - start) < milliSecond)
            {
                if (BlnReadCom == true)
                {
                    BlnReadCom = false;
                    return;
                }
                if (BlnStopCommand == true) return;
               
            }
        }


        private void disconnect_Click(object sender, RoutedEventArgs e)
        {   
            this.ClosePort();
            connection_textbox.Text = "disconnected";

        }
        public void ClosePort()
        {
            //****************************************************************
            //Function: ClosePort
            //Parameters: 
            //Description: close the connection
            //Return:
            //****************************************************************
            if (SCPort.IsOpen) SCPort.Close();
        }

        private void connection_textbox_TextChanged(object sender, TextChangedEventArgs e)
        {

        }

        private void portnumber_TextChanged(object sender, TextChangedEventArgs e)
        {

        }

        private void slValue_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {

        }

        private void slValue4_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {

        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {

        }

        private void slValue5_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {

        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            right.IsEnabled = false;
            if (!BlnConnect)
            {
                MessageBox.Show("please connect the port first!");
                return;
            }


            long lStep;
                string s;


                lStep = Convert.ToInt16((Convert.ToDouble(torun_textbox.Text)) / DblPulseEqui);
                if (lStep > 0)
                    s = "+" + lStep.ToString();
                else
                    s = lStep.ToString();
                StrReceiver = "";
                BlnBusy = true;
                BlnSet = true;
                SendCommand("X" + s + "\r");   //Move X axis to the appointed position.

                // timer1.IsEnabled = true;
                Delay(10000);

                BlnBusy = false;
                //  timer1.IsEnabled = false;
                inquire_current_position_X();
            right.IsEnabled = true;
            
        }



        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            DispatcherTimer timer1= new DispatcherTimer();
            SCPort = new SerialPort();
            
            sSpeed = 200;
            timer1.Start();

            #region camera intiation
            // for camera
            this.DataContext = this;
            GetVideoDevices();
            this.Closing += MainWindow_Closing;
            // end for camera
            #endregion camera intiation




        }


        private void Timer1_Tick(object sender, EventArgs e)
        {
           
        }

        private void TextBox_TextChanged_1(object sender, TextChangedEventArgs e)
        {
       
         
        }

        private void TextBox_TextChanged_2(object sender, TextChangedEventArgs e)
        {
           
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            sSpeed = Convert.ToInt16(speedtextbox.Text);
            if (BlnBusy == true)
            {
             //   MessageBox.Show("The connection is busy, please wait.", "Information");
                return;
            }
            if (!BlnConnect)
            {
                MessageBox.Show("please connect the port first!");
                return;
            }

            StrReceiver = "";
                BlnBusy = true;
                BlnSet = true;
                SendCommand("V" + sSpeed.ToString() + "\r");            //Set speed
                Delay(100000);
                BlnBusy = false;

                StrReceiver = "";
                BlnBusy = true;
                BlnSet = false;
                SendCommand("?V\r");                                    //Inquiry speed
                Delay(1000);
                BlnBusy = false;

                if (StrReceiver != "")
                {
                    sSpeed = Convert.ToInt16(System.Text.RegularExpressions.Regex.Replace(StrReceiver, @"[^0-9]+", ""));
                    speedshow.Text = sSpeed.ToString();
                   // stepvalueshow.Text = DblPulseEqui.ToString();
                }
            
        }

        private void Button_Click_3(object sender, RoutedEventArgs e)
        {
            far.IsEnabled = false;
            if (!BlnConnect)
            {
                MessageBox.Show("please connect the port first!");
                return;
            }


            long lStep;
                long lstep1;
                string s;


                lStep = Convert.ToInt16((Convert.ToDouble(torun_textbox.Text)) / DblPulseEqui);
                lstep1 = lStep;
                if (lstep1 > 0)
                    s = "+" + lStep.ToString();
                else
                    s = lstep1.ToString();

                StrReceiver = "";
                BlnBusy = true;
                BlnSet = true;
                SendCommand("Y" + s + "\r");   //Move X axis to the appointed position.

                // timer1.IsEnabled = true;
                Delay(10000);
                BlnBusy = false;
                //  timer1.IsEnabled = false;

                inquire_current_position_Y();
            far.IsEnabled = true;
            
        }
       
        private void timer1_Tick(object sender, EventArgs e)
        {

        }

        private void Button_Click_4(object sender, RoutedEventArgs e)
        {
            closer.IsEnabled = false;
            if (!BlnConnect)
            {
                MessageBox.Show("please connect the port first!");
                return;
            }


            long lStep;
                long lstep1;
                string s;


                lStep = Convert.ToInt16((Convert.ToDouble(torun_textbox.Text)) / DblPulseEqui);
                lstep1 = lStep * -1;
                if (lstep1 > 0)
                    s = "+" + lStep.ToString();
                else
                    s = lstep1.ToString();

                StrReceiver = "";
                BlnBusy = true;
                BlnSet = true;
                SendCommand("Y" + s + "\r");   //Move X axis to the appointed position.

                // timer1.IsEnabled = true;
                Delay(10000);
                BlnBusy = false;
                //  timer1.IsEnabled = false;

                inquire_current_position_Y();
            closer.IsEnabled = true;
            
        }

        private void torun_textbox_TextChanged(object sender, TextChangedEventArgs e)
        {

        }

        private void portnumber_TextChanged_1(object sender, TextChangedEventArgs e)
        {

        }

        private void place_TextChanged(object sender, TextChangedEventArgs e)
        {

        }

        
        private void stop_Click(object sender, RoutedEventArgs e)
        {
            if (BlnBusy == true)
            {
                MessageBox.Show("The connection is busy, please wait.", "Information");
                return;
            }
            if (!BlnConnect)
            {
                MessageBox.Show("please connect the port first!");
                return;
            }


            StrReceiver = "";
                BlnBusy = true;
                BlnSet = true;
                SendCommand("S\r");   //Stop moving
                Delay(10000);
                //                  timer1.IsEnabled = false;
                BlnStopCommand = true;
                //DelayWait(500);
                BlnBusy = false;
            
        }

        private void Button_Click_5(object sender, RoutedEventArgs e)
        {
            up.IsEnabled = false;
            if (!BlnConnect)
            {
                MessageBox.Show("please connect the port first!");
                return;
            }


            long lStep;
                long lstep1;
                string s;


                lStep = Convert.ToInt16((Convert.ToDouble(torun_textbox.Text)) / DblPulseEqui);
                lstep1 = lStep * -1;
                if (lstep1 > 0)
                    s = "+" + lstep1.ToString();
                else
                    s = lstep1.ToString();
                StrReceiver = "";
                BlnBusy = true;
                BlnSet = true;
                SendCommand("Z" + s + "\r");   //Move X axis to the appointed position.

                // timer1.IsEnabled = true;
                Delay(10000);
                BlnBusy = false;
                //  timer1.IsEnabled = false;

                inquire_current_position_Z();
            up.IsEnabled = true;
        }

        private void Button_Click_6(object sender, RoutedEventArgs e)
        {
            left.IsEnabled = false;
            if (!BlnConnect)
            {
                MessageBox.Show("please connect the port first!");
                return;
            }



            touchposition();
                long lStep;
                long lstep1;
                string s;


                lStep = Convert.ToInt32((Convert.ToDouble(torun_textbox.Text)) / DblPulseEqui);
                lstep1 = lStep * -1;
                if (lstep1 > 0)
                    s = "+" + lStep.ToString();
                else
                    s = lstep1.ToString();

                StrReceiver = "";
                BlnBusy = true;
                BlnSet = true;
                SendCommand("X" + s + "\r");   //Move X axis to the appointed position.

                // timer1.IsEnabled = true;
                Delay(10000);
                BlnBusy = false;
                //  timer1.IsEnabled = false;
                resetpostion();
                inquire_current_position_X();
            left.IsEnabled = true;
        }

        private void connection_textbox_TextChanged_1(object sender, TextChangedEventArgs e)
        {

        }

        private void Button_Click_7(object sender, RoutedEventArgs e)
        {

        }

        private void Button_Click_8(object sender, RoutedEventArgs e)
        {
            down.IsEnabled = true;
            if (!BlnConnect)
            {
                MessageBox.Show("please connect the port first!");
                return;
            }


            long lStep;
                long lstep1;
                string s;


                lStep = Convert.ToInt16((Convert.ToDouble(torun_textbox.Text)) / DblPulseEqui);
                lstep1 = lStep;
                if (lstep1 > 0)
                    s = "+" + lstep1.ToString();
                else
                    s = lstep1.ToString();

                StrReceiver = "";
                BlnBusy = true;
                BlnSet = true;
                SendCommand("Z" + s + "\r");   //Move X axis to the appointed position.

                // timer1.IsEnabled = true;
                Delay(10000);
                BlnBusy = false;
                //  timer1.IsEnabled = false;

                inquire_current_position_Z();
            down.IsEnabled = true;
            

        }

        private void Button_Click_9(object sender, RoutedEventArgs e)
        {
            resetpostion();
        }

        private void Button_Click_10(object sender, RoutedEventArgs e)
        {
            touchposition();
        }


        public void touchposition()
        {
            long lStep;
            long lstep1;
            string s;


            lStep = x_touch_position;
            lstep1 = lStep * 1;
            if (lstep1 > 0)
                s = "+" + lStep.ToString();
            else
                s = lstep1.ToString();

            StrReceiver = "";
            BlnBusy = true;
            BlnSet = true;
            SendCommand("Z" + s + "\r");   //Move X axis to the appointed position.

            // timer1.IsEnabled = true;
            Delay(1000);
            BlnBusy = false;
            //  timer1.IsEnabled = false;
        }

        public void resetpostion()
        {
            long lStep;
            long lstep1;
            string s;


            lStep = x_save_position;
            lstep1 = lStep * -1;
            if (lstep1 > 0)
                s = "+" + lStep.ToString();
            else
                s = lstep1.ToString();

            StrReceiver = "";
            BlnBusy = true;
            BlnSet = true;
            SendCommand("Z" + s + "\r");   //Move X axis to the appointed position.

            // timer1.IsEnabled = true;
            Delay(1000);
            BlnBusy = false;
            //  timer1.IsEnabled = false;
        }

       


      /*  public void inquire_current_position()
        {
            StrReceiver = "";
            BlnBusy = true;
            BlnSet = false;
            SendCommand("?X\r");            //Inquiry the current position of X axis
            Delay(100000);
            BlnBusy = false;

            if (StrReceiver != "")
            {
                try
                {


                    if (StrReceiver.Substring(5, 1) == "-")
                        lCurrStep = -Convert.ToInt64(System.Text.RegularExpressions.Regex.Replace(StrReceiver, @"[^0-9]+", ""));
                    else
                        lCurrStep = Convert.ToInt64(System.Text.RegularExpressions.Regex.Replace(StrReceiver, @"[^0-9]+", ""));
                }
                catch
                {
                    MessageBox.Show("failed to connect");
                }
            }
            else
                return;
            dCurrPosi = lCurrStep * DblPulseEqui;
            textbox7.Text = dCurrPosi.ToString();
        }

      */

        private void textbox7_TextChanged(object sender, TextChangedEventArgs e)
        {

        }

        private void Button_Click_12(object sender, RoutedEventArgs e)
        {
             x_save_position = Convert.ToInt16(Convert.ToDouble(dCurrPosi));
        }

        private void Button_Click_13(object sender, RoutedEventArgs e)
        {
            x_touch_position = Convert.ToInt16(Convert.ToDouble(dCurrPosi));
        }

        private void Button_Click_14(object sender, RoutedEventArgs e)
        {
           
           
        }

        private void printing_dots_Click(object sender, RoutedEventArgs e)
        {
            if (!BlnConnect)
            {
                MessageBox.Show("please connect the port first!");
                return;
            }
            double reseting_point = x_save_position;
            double touching_point = x_touch_position;

            int number_dot_X = Convert.ToInt16(number_of_dots_in_X_direction.Text);
            double distance_between_dots_X = Convert.ToDouble(distance_between_dots_in_X_direction.Text);

            double to_come_back_in_x_direction = number_dot_X * distance_between_dots_X * -1;



            int number_dots_Y = Convert.ToInt32(number_of_dots_in_Y_direction.Text);
            double distance_dots_Y = Convert.ToDouble(distance_between_dots_in_Y_direction.Text);

          


            #region finding distance between reset and touch points distance

            double touch_point = Convert.ToDouble(touch_point_textbox.Text);
            double reset_point = Convert.ToDouble(reset_position_textbox.Text);
            double travel_in_z_direction = touch_point - reset_point;
            double upside_travel_in_z_direction = travel_in_z_direction * -1;
            #endregion




            for (int y = 0; y < number_dots_Y; y++)
                {
                    //  Movement_z();
                    Delay(1000);

                    // number of dots
                    for (int xi = 0; xi < number_dot_X; xi++)
                    {
                    // distance between dots
                    //   moving_in_X_direction(distance_between_dots_X);

                    moving_in_Y_direction(distance_between_dots_X);
                        Delay(1000);

                         moving_in_Z_direction(travel_in_z_direction);
                        Delay(1000);


                         moving_in_Z_direction(upside_travel_in_z_direction);
                        Delay(1000);
                    }

                // come to first of next line
                moving_in_Y_direction(distance_dots_Y);
                Delay(1000);

                // to initial point of next line
                moving_in_X_direction(to_come_back_in_x_direction);
                Delay(1000);

            }




            // recieveing positions
            inquire_current_position_X();
            Delay(10000);
            inquire_current_position_Y();
            Delay(10000);
            inquire_current_position_Z();




        }



        private void printing_lines_Click(object sender, RoutedEventArgs e)
        {
            if (!BlnConnect)
            {
                MessageBox.Show("please connect the port first!");
                return;
            }
            double reseting_point = x_save_position;
            double touching_point = x_touch_position;

            int number_lines_X = Convert.ToInt16(number_of_lines_in_X_direction.Text);
            double distance_between_lines_X = Convert.ToDouble(distance_between_lines_in_X_direction.Text);
            double length_of_line = Convert.ToDouble(length_of_lines_in_X_direction.Text);

            double to_come_back_in_x_direction = number_lines_X *(distance_between_lines_X+length_of_line) * -1;

            int number_lines_Y = Convert.ToInt32(number_of_lines_in_Y_direction.Text);
            double distance_lines_Y = Convert.ToDouble(distance_between_lines_in_Y_direction.Text);




            #region finding distance between reset and touch points distance

            double touch_point = Convert.ToDouble(touch_point_textbox.Text);
            double reset_point = Convert.ToDouble(reset_position_textbox.Text);
            double travel_in_z_direction = touch_point - reset_point;
            double upside_travel_in_z_direction = travel_in_z_direction * -1;
            #endregion




            for (int y = 0; y < number_lines_Y; y++)
            {
                //  Movement_z();
                Delay(1000);

                // number of dots
                for (int xi = 0; xi < number_lines_X; xi++)
                {
                    // distance between dots
                    moving_in_X_direction(distance_between_lines_X);
                    Delay(1000);

                    moving_in_Z_direction(travel_in_z_direction);
                    Delay(1000);

                    moving_in_X_direction(length_of_line);


                    moving_in_Z_direction(upside_travel_in_z_direction);
                    Delay(1000);
                }

                // come to first of next line
                moving_in_Y_direction(distance_lines_Y);
                Delay(1000);

                // to initial point of next line
                moving_in_X_direction(to_come_back_in_x_direction);
                Delay(1000);

            }




            // recieveing positions
            inquire_current_position_X();
            Delay(10000);
            inquire_current_position_Y();
            Delay(10000);
            inquire_current_position_Z();
        }

        public void doting()
        {
            /*
            #region going down
            long lStep;
            long lstep1;
            string s;


            lStep = Convert.ToInt32(moving_distance / DblPulseEqui);
            lstep1 = lStep * +1;
            if (lstep1 > 0)
                s = "+" + lStep.ToString();
            else
                s = lstep1.ToString();

            StrReceiver = "";
            BlnBusy = true;
            BlnSet = true;
            SendCommand("Z" + s + "\r");   //Move X axis to the appointed position.

            // timer1.IsEnabled = true;
            Delay(1000);
            BlnBusy = false;
            //  timer1.IsEnabled = false;
            #endregion going down
            */


           /* #region coming up
            long lStep;
            long lstep1;
            string s;


            lStep = Convert.ToInt32(moving_distance / DblPulseEqui);
            lstep1 = lStep * +1;
            if (lstep1 > 0)
                s = "+" + lStep.ToString();
            else
                s = lstep1.ToString();

            StrReceiver = "";
            BlnBusy = true;
            BlnSet = true;
            SendCommand("Z" + s + "\r");   //Move X axis to the appointed position.

            // timer1.IsEnabled = true;
            Delay(1000);
            BlnBusy = false;
            //  timer1.IsEnabled = false;

            #endregion coming up
    */

        }


        public void moving_in_Y_direction(double moving_distance)
        {
            long lStep;
            long lstep1;
            string s;


            lStep = Convert.ToInt32(moving_distance / DblPulseEqui);
            lstep1 = lStep * +1;
            if (lstep1 > 0)
                s = "+" + lStep.ToString();
            else
                s = lstep1.ToString();

            StrReceiver = "";
            BlnBusy = true;
            BlnSet = true;
            SendCommand("Y" + s + "\r");   //Move X axis to the appointed position.

            // timer1.IsEnabled = true;
            Delay(1000);
            BlnBusy = false;
            //  timer1.IsEnabled = false;
        }




        public void moving_in_X_direction(double moving_distance)
        {
            long lStep;
            long lstep1;
            string s;


            lStep = Convert.ToInt32(moving_distance / DblPulseEqui);
            lstep1 = lStep * +1;
            if (lstep1 > 0)
                s = "+" + lStep.ToString();
            else
                s = lstep1.ToString();

            StrReceiver = "";
            BlnBusy = true;
            BlnSet = true;
            SendCommand("Z" + s + "\r");   //Move X axis to the appointed position.

            // timer1.IsEnabled = true;
            Delay(1000);
            BlnBusy = false;
            //  timer1.IsEnabled = false;
        }


        public void moving_in_Z_direction(double moving_distance)
        {
            long lStep;
            long lstep1;
            string s;


            lStep = Convert.ToInt32(moving_distance / DblPulseEqui);
            lstep1 = lStep * +1;
            if (lstep1 > 0)
                s = "+" + lStep.ToString();
            else
                s = lstep1.ToString();

            StrReceiver = "";
            BlnBusy = true;
            BlnSet = true;
            SendCommand("Z" + s + "\r");   //Move X axis to the appointed position.

            // timer1.IsEnabled = true;
            Delay(1000);
            BlnBusy = false;
            //  timer1.IsEnabled = false;
        }

        private void distance_between_dots_in_Y_direction_TextChanged(object sender, TextChangedEventArgs e)
        {

        }

        private void stepvalueshow_TextChanged(object sender, TextChangedEventArgs e)
        {

        }

        private void Button_Click_15(object sender, RoutedEventArgs e)
        {

        }

        private void Zero_XYZ_Click(object sender, RoutedEventArgs e)
        {
            if (BlnBusy == true)
            {
                MessageBox.Show("The connection is busy, please wait.", "Information");
                return;
            }
            if (BlnConnect)
            {


                StrReceiver = "";
                BlnBusy = true;
                BlnSet = true;
                SendCommand("HX0\r");   //Home X axis
                Delay(10000);
                // home to Y axis
                SendCommand("HY0\r");
                Delay(10000);

                // home to Z axis
                SendCommand("HZ0\r");
                Delay(100000);


                BlnBusy = false;
                inquire_current_position_X();
                inquire_current_position_Y();
                inquire_current_position_Z();
            }
        }

        private void position_Click(object sender, RoutedEventArgs e)
        {
            if (!BlnConnect)
            {
                MessageBox.Show("please connect the port first!");
                return;
            }
            if (BlnBusy == true)
            {
                MessageBox.Show("The connection is busy, please wait.", "Information");
                return;
            }
           
                inquire_current_position_X();
                inquire_current_position_Y();
                inquire_current_position_Z();
            
        }

       

       public void inquire_current_position_X()
        {
            StrReceiver = "";
            BlnBusy = true;
            BlnSet = false;
            SendCommand("?X\r");            //Inquiry the current position of X axis
            Delay(100000);
            BlnBusy = false;

            if (StrReceiver != "")
            {
                try
                {


                    if (StrReceiver.Substring(5, 1) == "-")
                        lCurrStep = -Convert.ToInt64(System.Text.RegularExpressions.Regex.Replace(StrReceiver, @"[^0-9]+", ""));
                    else
                        lCurrStep = Convert.ToInt64(System.Text.RegularExpressions.Regex.Replace(StrReceiver, @"[^0-9]+", ""));
                }
                catch
                {
                    MessageBox.Show("failed to connect");
                }
            }
            else
                return;
            dCurrPosi = lCurrStep * DblPulseEqui;
            textbox7.Text = dCurrPosi.ToString();
        }

        public void inquire_current_position_Y()
        {
            StrReceiver = "";
            BlnBusy = true;
            BlnSet = false;
            SendCommand("?Y\r");            //Inquiry the current position of X axis
            Delay(100000);
            BlnBusy = false;

            if (StrReceiver != "")
            {
                try
                {


                    if (StrReceiver.Substring(5, 1) == "-")
                        lCurrStep = -Convert.ToInt64(System.Text.RegularExpressions.Regex.Replace(StrReceiver, @"[^0-9]+", ""));
                    else
                        lCurrStep = Convert.ToInt64(System.Text.RegularExpressions.Regex.Replace(StrReceiver, @"[^0-9]+", ""));
                }
                catch
                {
                    MessageBox.Show("failed to connect");
                }
            }
            else
                return;
            dCurrPosi = lCurrStep * DblPulseEqui;
            Y_position_textbox.Text = dCurrPosi.ToString();
        }

        public void inquire_current_position_Z()
        {
            StrReceiver = "";
            BlnBusy = true;
            BlnSet = false;
            SendCommand("?Z\r");            //Inquiry the current position of X axis
            Delay(100000);
            BlnBusy = false;

            if (StrReceiver != "")
            {
                try
                {


                    if (StrReceiver.Substring(5, 1) == "-")
                        lCurrStep = -Convert.ToInt64(System.Text.RegularExpressions.Regex.Replace(StrReceiver, @"[^0-9]+", ""));
                    else
                        lCurrStep = Convert.ToInt64(System.Text.RegularExpressions.Regex.Replace(StrReceiver, @"[^0-9]+", ""));
                }
                catch
                {
                    MessageBox.Show("failed to connect");
                }
            }
            else
                return;
            dCurrPosi = lCurrStep * DblPulseEqui;
            Z_position_textbox.Text = dCurrPosi.ToString();
        }

        private void reset_position_button_Click(object sender, RoutedEventArgs e)
        {
           
            reset_position_textbox.Text = Z_position_textbox.Text;
        }

        private void touch_position_Click(object sender, RoutedEventArgs e)
        {
            touch_point_textbox.Text = Z_position_textbox.Text;
        }

        private void Button_Click_11()
        {

        }

        private void Button_Click_11(object sender, RoutedEventArgs e)
        {
            if (_videoSource != null && _videoSource.IsRunning)
            {
                _videoSource.SignalToStop();
                _videoSource.NewFrame -= new NewFrameEventHandler(video_NewFrame);
            }
        }

      /*  private void StartCamera()
        {
            if (CurrentDevice != null)
            {
                _videoSource = new VideoCaptureDevice(CurrentDevice.MonikerString);
                _videoSource.NewFrame += video_NewFrame;
                _videoSource.Start();
            }
        }

    */





        private void Button_Click_16(object sender, RoutedEventArgs e)
        {

        }

        private void camera_connect(object sender, RoutedEventArgs e)
        {
            StartCamera();
        }

       

        #region camera
        // camera
        #region Public properties

        public ObservableCollection<FilterInfo> VideoDevices { get; set; }

        public FilterInfo CurrentDevice
        {
            get { return _currentDevice; }
            set { _currentDevice = value; this.OnPropertyChanged("CurrentDevice"); }
        }
        private FilterInfo _currentDevice;

        #endregion


        #region Private fields

        private IVideoSource _videoSource;

        #endregion



        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            StopCamera();
        }

      

        private void video_NewFrame(object sender, AForge.Video.NewFrameEventArgs eventArgs)
        {
            try
            {
                BitmapImage bi;
                using (var bitmap = (Bitmap)eventArgs.Frame.Clone())
                {
                    bi = bitmap.ToBitmapImage();
                }
                bi.Freeze(); // avoid cross thread operations and prevents leaks
                Dispatcher.BeginInvoke(new ThreadStart(delegate { videoPlayer.Source = bi; }));
            }
            catch (Exception exc)
            {
                MessageBox.Show("Error on _videoSource_NewFrame:\n" + exc.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                StopCamera();
            }
        }

      

        private void GetVideoDevices()
        {
            VideoDevices = new ObservableCollection<FilterInfo>();
            foreach (FilterInfo filterInfo in new FilterInfoCollection(FilterCategory.VideoInputDevice))
            {
                VideoDevices.Add(filterInfo);
            }
            if (VideoDevices.Any())
            {
                CurrentDevice = VideoDevices[0];
            }
            else
            {
                MessageBox.Show("No video sources found", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void StartCamera()
        {
            if (CurrentDevice != null)
            {
                _videoSource = new VideoCaptureDevice(CurrentDevice.MonikerString);
                _videoSource.NewFrame += video_NewFrame;
                _videoSource.Start();
            }
        }

        private void StopCamera()
        {
            if (_videoSource != null && _videoSource.IsRunning)
            {
                _videoSource.SignalToStop();
                _videoSource.NewFrame -= new NewFrameEventHandler(video_NewFrame);
            }
        }

        #region INotifyPropertyChanged members

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChangedEventHandler handler = this.PropertyChanged;
            if (handler != null)
            {
                var e = new PropertyChangedEventArgs(propertyName);
                handler(this, e);
            }
        }
        #endregion
        // end camera
        #endregion camera

        private void comboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void number_of_lines_in_X_direction_TextChanged(object sender, TextChangedEventArgs e)
        {

        }

        private void portnumber_TextChanged_2(object sender, TextChangedEventArgs e)
        {

        }

        private void Z_position_textbox_TextChanged(object sender, TextChangedEventArgs e)
        {

        }

        private void fivemicrometer_1_stepsize_Click(object sender, RoutedEventArgs e)
        {
            if (BlnBusy == true)
            {
                MessageBox.Show("The connection is busy, please wait.", "Information");
                return;
            }
            if (BlnConnect)
            {


                DblPulseEqui = Math.Round(Convert.ToDouble(1.8) / (180), 5);
                stepvalueshow.Text = "step size is 5 micro meter";
            }
        }

        private void tenmicrometer_2_stepsize_Click(object sender, RoutedEventArgs e)
        {
            if (BlnBusy == true)
            {
                MessageBox.Show("The connection is busy, please wait.", "Information");
                return;
            }
            if (BlnConnect)
            {


                DblPulseEqui = Math.Round(Convert.ToDouble(1.8) / ((Convert.ToDouble(180) * Convert.ToDouble(2))), 5);
                stepvalueshow.Text = "stepsize is ten micro meter";
            }
        }

        private void twentymicrometer_4stepsize_Click(object sender, RoutedEventArgs e)
        {
            if (BlnBusy == true)
            {
                MessageBox.Show("The connection is busy, please wait.", "Information");
                return;
            }
            if (BlnConnect)
            {


                DblPulseEqui = Math.Round(Convert.ToDouble(1.8) / ((Convert.ToDouble(180) * Convert.ToDouble(4))), 5);
                stepvalueshow.Text = "step size is 20 micro meter";
            }
        }

        private void fortymicrometer_8stepsize_Click(object sender, RoutedEventArgs e)
        {
            if (BlnBusy == true)
            {
                MessageBox.Show("The connection is busy, please wait.", "Information");
                return;
            }
            if (BlnConnect)
            {


                DblPulseEqui = Math.Round(Convert.ToDouble(1.8) / ((Convert.ToDouble(180) * Convert.ToDouble(8))), 5);
                stepvalueshow.Text = "stepsize is 40 micro meter";
            }
        }
    }
}
