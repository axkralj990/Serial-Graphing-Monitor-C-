﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO.Ports;
using ZedGraph;
using System.Diagnostics;
using System.IO;

namespace SerialPlotter_AleksijKraljic
{
    public partial class Form1 : Form
    {
        Measurement measurement = new Measurement();
        List<Channel> channels = new List<Channel>();

        string fileName = "measured_data.txt";

        // stopwatch for recording timestamp
        Stopwatch s_watch = new Stopwatch();

        List<string> write_D = new List<string>();

        Color[] lineColors = { Color.Blue, Color.Red, Color.Green, Color.Black, Color.Purple, Color.Pink };

        // new GraphPane for plotting
        GraphPane akMonitor = new GraphPane();
        // range of X-Axis
        double t_range = 4;
        // Min and Max values for Y-Axis
        double Y_max = 5;
        double Y_min = 0;

        public Form1()
        {
            InitializeComponent();
            
            // initial form object states
            btn_connect.Enabled = false;
            btn_disconnect.Enabled = false;
            btn_start.Enabled = false;
            btn_stop.Enabled = false;

            // get and set serial ports
            getAndWritePorts();

            // available baud rates
            string[] bauds = { "300", "1200", "2400", "4800", "9600", "19200", "38400", "57600", "74880", "115200", "230400", "250000"};
            baudBox.DataSource = bauds;
            baudBox.SelectedIndex = 4;

            fileNameBox.Text = fileName;

            // initial form object states
            checkCh1.Enabled = true;
            checkCh2.Enabled = true;
            checkCh3.Enabled = true;
            checkCh4.Enabled = true;
            checkCh5.Enabled = true;
            checkCh6.Enabled = true;
            checkAutoY.Checked = true;
            numericUDmaxY.Enabled = false;
            numericUDminY.Enabled = false;
            numericUDtime.Enabled = true;

            // graph apearance settings
            akMonitor = zedGraphControl1.GraphPane;
            akMonitor.Title.Text = "";
            akMonitor.XAxis.Title.Text = "";
            akMonitor.YAxis.Title.Text = "";
            akMonitor.XAxis.MajorGrid.IsVisible = true;
            akMonitor.YAxis.MajorGrid.IsVisible = true;

            fileNameBox.Enabled = false;

            for (int i = 0; i < 6; i++)
            {
                channels.Add(new Channel(i));
            }

        }

        private void btn_refreshCOM_Click(object sender, EventArgs e)
        {
            getAndWritePorts();
        }

        private void getAndWritePorts()
        {
            // get port names
            string[] avports = SerialPort.GetPortNames();

            // write them to select box
            comBox.DataSource = avports;
            if (comBox.Items.Count > 0)
            {
                // select the first comport 
                comBox.SelectedIndex = 0;
            }
        }

        private void btn_connect_Click(object sender, EventArgs e)
        {
            serialPort1.PortName = comBox.Text;
            serialPort1.BaudRate = int.Parse(baudBox.SelectedItem.ToString());

            try
            {
                serialPort1.Open();
            }
            catch
            {
                MessageBox.Show("No devices found");
            }

            if (serialPort1.IsOpen)
            {
                btn_connect.Enabled = false;
                btn_disconnect.Enabled = true;
                btn_start.Enabled = true;
                btn_stop.Enabled = false;
                comBox.Enabled = false;
                baudBox.Enabled = false;
                btn_refreshCOM.Enabled = false;
                serialPort1.DataReceived += new SerialDataReceivedEventHandler(SerialPort1_DataReceived);
                serialPort1.Write("b");
                checkCh1.Enabled = true;
                checkCh2.Enabled = true;
                checkCh3.Enabled = true;
                checkCh4.Enabled = true;
            }

        }

        private void SerialPort1_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
                if (serialPort1.IsOpen)
                {
                    measurement.RxString += serialPort1.ReadExisting();
                    measurement.RxStringComplete = false;
                    measurement.cleanUpReceivedData();
                }

                if (measurement.RxStringComplete == true)
                {
                    measurement.splitReceivedString();

                    channels.ForEach(c => c.timeStamp = measurement.timeStamp);
                    channels.ForEach(c => c.splittedData = measurement.splittedData);

                    this.BeginInvoke(new EventHandler(toBuffer));

                    if (saveCheckBox.Checked)
                    {
                        try
                        {
                            channels.ForEach(c => c.recordData());
                        }
                        catch { }
                    }

                    measurement.clearRxString();  
                }
        }

        private void btn_start_Click(object sender, EventArgs e)
        {
            // button that starts the communication
            serialPort1.Write("a");
            btn_start.Enabled = false;
            btn_stop.Enabled = true;
            btn_disconnect.Enabled = false;

            timer1.Start();
            s_watch.Start();

            akMonitor.CurveList.Clear();

            channels.ForEach(c => c.clearOnStart());

            for (int i = 0; i < 6; i++)
            {
                channels[i].lineColor = lineColors[i];
                channels[i].curve = akMonitor.AddCurve(null, channels[i].ringBuffer, channels[i].lineColor, SymbolType.None);
                channels[i].setLineWidth(1);
            }

        }

        private void btn_stop_Click(object sender, EventArgs e)
        {
            // button that stops the communication
            serialPort1.Write("b");
            btn_stop.Enabled = false;
            btn_start.Enabled = true;
            btn_disconnect.Enabled = true;

            timer1.Stop();
            s_watch.Stop();
            s_watch.Reset();

            if (saveCheckBox.Checked)
            {
                save_measurements();
            }
        }

        private void save_measurements()
        {
            // method used to store recorded data to file
            string folder_path = System.IO.Path.GetDirectoryName(Application.ExecutablePath);
            
            //string fileName = DateTime.Now.ToString(@"MM\/dd\/yyyy h\:mm tt");

            string path = folder_path + "\\" + fileNameBox.Text;

            using (StreamWriter sw = File.CreateText(path))
            {
                sw.WriteLine("=====measurements=====");
                sw.WriteLine("|t|ch1|ch2|ch3|ch4|");

                for (int i=0;i<(channels[0].recordedTime.Count);i++)
                {
                    try
                    {
                        write_D.Add(Convert.ToString(channels[0].recordedTime[i]) + "," + Convert.ToString(channels[0].recordedValues[i]));
                    }
                    catch
                    {
                        write_D.Add("outOfRange");
                    }
                }

                foreach (string line in write_D)
                {
                    sw.WriteLine(line);
                }
            }
        }

        private void btn_disconnect_Click(object sender, EventArgs e)
        {
            // button to disconnect from the device
            if (serialPort1.IsOpen)
            {
                serialPort1.Close();

                btn_connect.Enabled = true;
                btn_disconnect.Enabled = false;
                btn_start.Enabled = false;
                btn_stop.Enabled = false;
            }

            checkCh1.Enabled = false;
            checkCh2.Enabled = false;
            checkCh3.Enabled = false;
            checkCh4.Enabled = false;
            comBox.Enabled = true;
            baudBox.Enabled = true;
            btn_refreshCOM.Enabled = true;

        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            // tasks to perform when the form is being closed
            if (serialPort1.IsOpen) serialPort1.Write("b");
            System.Threading.Thread.Sleep(100);

            DialogResult dialogC = MessageBox.Show("Are you sure you want to exit?","Exit",MessageBoxButtons.YesNo);
            if (dialogC == DialogResult.Yes)
            {
                if (serialPort1.IsOpen) serialPort1.Close();
                Application.ExitThread();
            }
            else if (dialogC == DialogResult.No)
            {
                e.Cancel = true;
            }
            
        }

        
        private void comBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comBox.SelectedItem == null)
            {
                btn_connect.Enabled = false;
            }
            else
            {
                btn_connect.Enabled = true;
            }
        }

        private void toBuffer(object sender, EventArgs e)
        {
            measurement.timeStamp = Convert.ToDouble(s_watch.ElapsedMilliseconds);
            try
            {
                if (checkCh1.Checked) { channels[0].addToBuffer(); }
                else { channels[0].ringBuffer.Clear(); }
                if (checkCh2.Checked && measurement.numOfDataReceived >= 2) { channels[1].addToBuffer(); }
                else { channels[1].ringBuffer.Clear(); }
            }
            catch { }
        }

        private void plot_data(object sender, EventArgs e)
        {
            // method to plot the received data
            zedGraphControl1.AxisChange();
            zedGraphControl1.Refresh();
            akMonitor.XAxis.Scale.Min = measurement.timeStamp / 1000 - t_range;
            akMonitor.XAxis.Scale.Max = measurement.timeStamp / 1000;

            if (!checkAutoY.Checked)
            {
                akMonitor.YAxis.Scale.Max = Y_max;
                akMonitor.YAxis.Scale.Min = Y_min;
            }
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            // timer that updates the graph
            this.BeginInvoke(new EventHandler(plot_data));
        }
        
        private void checkAutoY_CheckedChanged(object sender, EventArgs e)
        {
            if (checkAutoY.Checked)
            {
                akMonitor.YAxis.Scale.MaxAuto = true;
                akMonitor.YAxis.Scale.MinAuto = true;
                numericUDmaxY.Enabled = false;
                numericUDminY.Enabled = false;
            }
            else
            {
                akMonitor.YAxis.Scale.MaxAuto = false;
                akMonitor.YAxis.Scale.MinAuto = false;
                numericUDmaxY.Enabled = true;
                numericUDminY.Enabled = true;
            }
        }

        private void numericUDtime_ValueChanged(object sender, EventArgs e)
        {
            t_range = Convert.ToDouble(numericUDtime.Value);
        }

        private void numericUDmaxY_ValueChanged(object sender, EventArgs e)
        {
            Y_max = Convert.ToDouble(numericUDmaxY.Value);
        }

        private void numericUDminY_ValueChanged(object sender, EventArgs e)
        {
            Y_min = Convert.ToDouble(numericUDminY.Value);
        }

        private void configDirectionsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AboutBox1 aboutWindow = new AboutBox1();
            aboutWindow.Show();
        }

        private void saveCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (saveCheckBox.Checked == true)
            {
                fileNameBox.Enabled = true;
            }
            else if (saveCheckBox.Checked == false)
            {
                fileNameBox.Enabled = false;
            }
        }
    }
}
