namespace FnirsExe
{
    partial class Software
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.Button btnTestVirtual;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.panel1 = new System.Windows.Forms.Panel();
            this.cbStop = new System.Windows.Forms.ComboBox();
            this.label5 = new System.Windows.Forms.Label();
            this.cbParity = new System.Windows.Forms.ComboBox();
            this.label4 = new System.Windows.Forms.Label();
            this.cbData = new System.Windows.Forms.ComboBox();
            this.label3 = new System.Windows.Forms.Label();
            this.cbBaud = new System.Windows.Forms.ComboBox();
            this.label2 = new System.Windows.Forms.Label();
            this.cbPort = new System.Windows.Forms.ComboBox();
            this.label1 = new System.Windows.Forms.Label();
            this.btnSearchPort = new System.Windows.Forms.Button();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.cb8 = new System.Windows.Forms.CheckBox();
            this.cb6 = new System.Windows.Forms.CheckBox();
            this.cb7 = new System.Windows.Forms.CheckBox();
            this.cb5 = new System.Windows.Forms.CheckBox();
            this.cb4 = new System.Windows.Forms.CheckBox();
            this.cb2 = new System.Windows.Forms.CheckBox();
            this.cb3 = new System.Windows.Forms.CheckBox();
            this.cb1 = new System.Windows.Forms.CheckBox();
            this.panel2 = new System.Windows.Forms.Panel();
            this.btnShowImage = new System.Windows.Forms.Button();
            this.btnSavedata = new System.Windows.Forms.Button();
            this.btnClear = new System.Windows.Forms.Button();
            this.btnCollect = new System.Windows.Forms.Button();
            this.btnOpen = new System.Windows.Forms.Button();
            this.rb16 = new System.Windows.Forms.RadioButton();
            this.rbString = new System.Windows.Forms.RadioButton();
            this.gbReceive = new System.Windows.Forms.GroupBox();
            this.tbAge = new System.Windows.Forms.TextBox();
            this.tbGender = new System.Windows.Forms.TextBox();
            this.lbAge = new System.Windows.Forms.Label();
            this.lbGender = new System.Windows.Forms.Label();
            this.tbUname = new System.Windows.Forms.TextBox();
            this.label6 = new System.Windows.Forms.Label();
            this.tbReceive = new System.Windows.Forms.TextBox();
            this.OpenBrainViewer = new System.Windows.Forms.Button();
            this.panel1.SuspendLayout();
            this.groupBox1.SuspendLayout();
            this.panel2.SuspendLayout();
            this.gbReceive.SuspendLayout();
            this.SuspendLayout();
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.cbStop);
            this.panel1.Controls.Add(this.label5);
            this.panel1.Controls.Add(this.cbParity);
            this.panel1.Controls.Add(this.label4);
            this.panel1.Controls.Add(this.cbData);
            this.panel1.Controls.Add(this.label3);
            this.panel1.Controls.Add(this.cbBaud);
            this.panel1.Controls.Add(this.label2);
            this.panel1.Controls.Add(this.cbPort);
            this.panel1.Controls.Add(this.label1);
            this.panel1.Location = new System.Drawing.Point(21, 86);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(339, 292);
            this.panel1.TabIndex = 0;
            // 
            // cbStop
            // 
            this.cbStop.Font = new System.Drawing.Font("宋体", 12F);
            this.cbStop.FormattingEnabled = true;
            this.cbStop.Items.AddRange(new object[] {
            "1",
            "2"});
            this.cbStop.Location = new System.Drawing.Point(124, 233);
            this.cbStop.Name = "cbStop";
            this.cbStop.Size = new System.Drawing.Size(185, 32);
            this.cbStop.TabIndex = 9;
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Font = new System.Drawing.Font("宋体", 12F);
            this.label5.Location = new System.Drawing.Point(24, 241);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(94, 24);
            this.label5.TabIndex = 8;
            this.label5.Text = "停止位:";
            // 
            // cbParity
            // 
            this.cbParity.Font = new System.Drawing.Font("宋体", 12F);
            this.cbParity.FormattingEnabled = true;
            this.cbParity.Items.AddRange(new object[] {
            "None",
            "Odd",
            "Even"});
            this.cbParity.Location = new System.Drawing.Point(124, 181);
            this.cbParity.Name = "cbParity";
            this.cbParity.Size = new System.Drawing.Size(185, 32);
            this.cbParity.TabIndex = 7;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Font = new System.Drawing.Font("宋体", 12F);
            this.label4.Location = new System.Drawing.Point(24, 189);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(94, 24);
            this.label4.TabIndex = 6;
            this.label4.Text = "校验位:";
            // 
            // cbData
            // 
            this.cbData.Font = new System.Drawing.Font("宋体", 12F);
            this.cbData.FormattingEnabled = true;
            this.cbData.Items.AddRange(new object[] {
            "7",
            "8"});
            this.cbData.Location = new System.Drawing.Point(124, 132);
            this.cbData.Name = "cbData";
            this.cbData.Size = new System.Drawing.Size(185, 32);
            this.cbData.TabIndex = 5;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Font = new System.Drawing.Font("宋体", 12F);
            this.label3.Location = new System.Drawing.Point(24, 140);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(94, 24);
            this.label3.TabIndex = 4;
            this.label3.Text = "数据位:";
            // 
            // cbBaud
            // 
            this.cbBaud.Font = new System.Drawing.Font("宋体", 12F);
            this.cbBaud.FormattingEnabled = true;
            this.cbBaud.Items.AddRange(new object[] {
            "9600",
            "19200",
            "38400",
            "57600",
            "115200",
            "921600",
            "460800"});
            this.cbBaud.Location = new System.Drawing.Point(124, 83);
            this.cbBaud.Name = "cbBaud";
            this.cbBaud.Size = new System.Drawing.Size(185, 32);
            this.cbBaud.TabIndex = 3;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Font = new System.Drawing.Font("宋体", 12F);
            this.label2.Location = new System.Drawing.Point(24, 91);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(94, 24);
            this.label2.TabIndex = 2;
            this.label2.Text = "波特率:";
            // 
            // cbPort
            // 
            this.cbPort.Font = new System.Drawing.Font("宋体", 12F);
            this.cbPort.FormattingEnabled = true;
            this.cbPort.Items.AddRange(new object[] {
            "COM1",
            "COM2",
            "COM3",
            "COM4",
            "COM5",
            "COM6",
            "COM7",
            "COM8",
            "COM9"});
            this.cbPort.Location = new System.Drawing.Point(124, 30);
            this.cbPort.Name = "cbPort";
            this.cbPort.Size = new System.Drawing.Size(185, 32);
            this.cbPort.TabIndex = 1;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("宋体", 12F);
            this.label1.Location = new System.Drawing.Point(24, 38);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(94, 24);
            this.label1.TabIndex = 0;
            this.label1.Text = "串口号:";
            // 
            // btnSearchPort
            // 
            this.btnSearchPort.Font = new System.Drawing.Font("宋体", 12F);
            this.btnSearchPort.Location = new System.Drawing.Point(73, 23);
            this.btnSearchPort.Name = "btnSearchPort";
            this.btnSearchPort.Size = new System.Drawing.Size(207, 44);
            this.btnSearchPort.TabIndex = 1;
            this.btnSearchPort.Text = "搜索串口";
            this.btnSearchPort.UseVisualStyleBackColor = true;
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.cb8);
            this.groupBox1.Controls.Add(this.cb6);
            this.groupBox1.Controls.Add(this.cb7);
            this.groupBox1.Controls.Add(this.cb5);
            this.groupBox1.Controls.Add(this.cb4);
            this.groupBox1.Controls.Add(this.cb2);
            this.groupBox1.Controls.Add(this.cb3);
            this.groupBox1.Controls.Add(this.cb1);
            this.groupBox1.Font = new System.Drawing.Font("宋体", 12F);
            this.groupBox1.Location = new System.Drawing.Point(21, 408);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(339, 138);
            this.groupBox1.TabIndex = 2;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "通道选择";
            // 
            // cb8
            // 
            this.cb8.AutoSize = true;
            this.cb8.Location = new System.Drawing.Point(261, 85);
            this.cb8.Name = "cb8";
            this.cb8.Size = new System.Drawing.Size(72, 28);
            this.cb8.TabIndex = 7;
            this.cb8.Text = "CH8";
            this.cb8.UseVisualStyleBackColor = true;
            // 
            // cb6
            // 
            this.cb6.AutoSize = true;
            this.cb6.Location = new System.Drawing.Point(95, 85);
            this.cb6.Name = "cb6";
            this.cb6.Size = new System.Drawing.Size(72, 28);
            this.cb6.TabIndex = 6;
            this.cb6.Text = "CH6";
            this.cb6.UseVisualStyleBackColor = true;
            // 
            // cb7
            // 
            this.cb7.AutoSize = true;
            this.cb7.Location = new System.Drawing.Point(183, 85);
            this.cb7.Name = "cb7";
            this.cb7.Size = new System.Drawing.Size(72, 28);
            this.cb7.TabIndex = 5;
            this.cb7.Text = "CH7";
            this.cb7.UseVisualStyleBackColor = true;
            // 
            // cb5
            // 
            this.cb5.AutoSize = true;
            this.cb5.Location = new System.Drawing.Point(17, 86);
            this.cb5.Name = "cb5";
            this.cb5.Size = new System.Drawing.Size(72, 28);
            this.cb5.TabIndex = 4;
            this.cb5.Text = "CH5";
            this.cb5.UseVisualStyleBackColor = true;
            // 
            // cb4
            // 
            this.cb4.AutoSize = true;
            this.cb4.Location = new System.Drawing.Point(261, 34);
            this.cb4.Name = "cb4";
            this.cb4.Size = new System.Drawing.Size(72, 28);
            this.cb4.TabIndex = 3;
            this.cb4.Text = "CH4";
            this.cb4.UseVisualStyleBackColor = true;
            // 
            // cb2
            // 
            this.cb2.AutoSize = true;
            this.cb2.Location = new System.Drawing.Point(95, 35);
            this.cb2.Name = "cb2";
            this.cb2.Size = new System.Drawing.Size(72, 28);
            this.cb2.TabIndex = 2;
            this.cb2.Text = "CH2";
            this.cb2.UseVisualStyleBackColor = true;
            // 
            // cb3
            // 
            this.cb3.AutoSize = true;
            this.cb3.Location = new System.Drawing.Point(183, 34);
            this.cb3.Name = "cb3";
            this.cb3.Size = new System.Drawing.Size(72, 28);
            this.cb3.TabIndex = 1;
            this.cb3.Text = "CH3";
            this.cb3.UseVisualStyleBackColor = true;
            // 
            // cb1
            // 
            this.cb1.AutoSize = true;
            this.cb1.Location = new System.Drawing.Point(17, 35);
            this.cb1.Name = "cb1";
            this.cb1.Size = new System.Drawing.Size(72, 28);
            this.cb1.TabIndex = 0;
            this.cb1.Text = "CH1";
            this.cb1.UseVisualStyleBackColor = true;
            // 
            // panel2
            // 
            this.panel2.Controls.Add(this.OpenBrainViewer);
            this.panel2.Controls.Add(this.btnShowImage);
            this.panel2.Controls.Add(this.btnSavedata);
            this.panel2.Controls.Add(this.btnClear);
            this.panel2.Controls.Add(this.btnCollect);
            this.panel2.Controls.Add(this.btnOpen);
            this.panel2.Controls.Add(this.rb16);
            this.panel2.Controls.Add(this.rbString);
            this.panel2.Location = new System.Drawing.Point(21, 577);
            this.panel2.Name = "panel2";
            this.panel2.Size = new System.Drawing.Size(351, 319);
            this.panel2.TabIndex = 3;
            // 
            // btnShowImage
            // 
            this.btnShowImage.Font = new System.Drawing.Font("宋体", 12F);
            this.btnShowImage.Location = new System.Drawing.Point(23, 131);
            this.btnShowImage.Name = "btnShowImage";
            this.btnShowImage.Size = new System.Drawing.Size(126, 39);
            this.btnShowImage.TabIndex = 4;
            this.btnShowImage.Text = "波形图像";
            this.btnShowImage.UseVisualStyleBackColor = true;
            this.btnShowImage.Click += new System.EventHandler(this.btnShowImage_Click);
            // 
            // btnSavedata
            // 
            this.btnSavedata.Font = new System.Drawing.Font("宋体", 12F);
            this.btnSavedata.Location = new System.Drawing.Point(183, 131);
            this.btnSavedata.Name = "btnSavedata";
            this.btnSavedata.Size = new System.Drawing.Size(126, 39);
            this.btnSavedata.TabIndex = 5;
            this.btnSavedata.Text = "保存数据";
            this.btnSavedata.UseVisualStyleBackColor = true;
            // 
            // btnClear
            // 
            this.btnClear.Font = new System.Drawing.Font("宋体", 12F);
            this.btnClear.Location = new System.Drawing.Point(23, 197);
            this.btnClear.Name = "btnClear";
            this.btnClear.Size = new System.Drawing.Size(131, 39);
            this.btnClear.TabIndex = 3;
            this.btnClear.Text = "清空接收";
            this.btnClear.UseVisualStyleBackColor = true;
            this.btnClear.Click += new System.EventHandler(this.btnClear_Click);
            // 
            // btnCollect
            // 
            this.btnCollect.Font = new System.Drawing.Font("宋体", 12F);
            this.btnCollect.Location = new System.Drawing.Point(183, 64);
            this.btnCollect.Name = "btnCollect";
            this.btnCollect.Size = new System.Drawing.Size(126, 39);
            this.btnCollect.TabIndex = 4;
            this.btnCollect.Text = "AD采集";
            this.btnCollect.UseVisualStyleBackColor = true;
            this.btnCollect.Click += new System.EventHandler(this.btnCollect_Click);
            // 
            // btnOpen
            // 
            this.btnOpen.Font = new System.Drawing.Font("宋体", 12F);
            this.btnOpen.Location = new System.Drawing.Point(23, 64);
            this.btnOpen.Name = "btnOpen";
            this.btnOpen.Size = new System.Drawing.Size(126, 39);
            this.btnOpen.TabIndex = 3;
            this.btnOpen.Text = "打开串口";
            this.btnOpen.UseVisualStyleBackColor = true;
            this.btnOpen.Click += new System.EventHandler(this.btnOpen_Click);
            // 
            // rb16
            // 
            this.rb16.AutoSize = true;
            this.rb16.Font = new System.Drawing.Font("宋体", 12F);
            this.rb16.Location = new System.Drawing.Point(183, 18);
            this.rb16.Name = "rb16";
            this.rb16.Size = new System.Drawing.Size(107, 28);
            this.rb16.TabIndex = 1;
            this.rb16.TabStop = true;
            this.rb16.Text = "16进制";
            this.rb16.UseVisualStyleBackColor = true;
            // 
            // rbString
            // 
            this.rbString.AutoSize = true;
            this.rbString.Font = new System.Drawing.Font("宋体", 12F);
            this.rbString.Location = new System.Drawing.Point(28, 18);
            this.rbString.Name = "rbString";
            this.rbString.Size = new System.Drawing.Size(107, 28);
            this.rbString.TabIndex = 0;
            this.rbString.TabStop = true;
            this.rbString.Text = "字符串";
            this.rbString.UseVisualStyleBackColor = true;
            // 
            // gbReceive
            // 
            this.gbReceive.Controls.Add(this.tbAge);
            this.gbReceive.Controls.Add(this.tbGender);
            this.gbReceive.Controls.Add(this.lbAge);
            this.gbReceive.Controls.Add(this.lbGender);
            this.gbReceive.Controls.Add(this.tbUname);
            this.gbReceive.Controls.Add(this.label6);
            this.gbReceive.Controls.Add(this.tbReceive);
            this.gbReceive.Font = new System.Drawing.Font("宋体", 12F);
            this.gbReceive.Location = new System.Drawing.Point(382, 72);
            this.gbReceive.Name = "gbReceive";
            this.gbReceive.Size = new System.Drawing.Size(640, 824);
            this.gbReceive.TabIndex = 4;
            this.gbReceive.TabStop = false;
            this.gbReceive.Text = "接收数据";
            // 
            // tbAge
            // 
            this.tbAge.Font = new System.Drawing.Font("宋体", 12F);
            this.tbAge.Location = new System.Drawing.Point(199, 675);
            this.tbAge.Name = "tbAge";
            this.tbAge.Size = new System.Drawing.Size(286, 35);
            this.tbAge.TabIndex = 15;
            // 
            // tbGender
            // 
            this.tbGender.Font = new System.Drawing.Font("宋体", 12F);
            this.tbGender.Location = new System.Drawing.Point(199, 625);
            this.tbGender.Name = "tbGender";
            this.tbGender.Size = new System.Drawing.Size(286, 35);
            this.tbGender.TabIndex = 14;
            // 
            // lbAge
            // 
            this.lbAge.AutoSize = true;
            this.lbAge.Font = new System.Drawing.Font("宋体", 12F);
            this.lbAge.Location = new System.Drawing.Point(39, 686);
            this.lbAge.Name = "lbAge";
            this.lbAge.Size = new System.Drawing.Size(118, 24);
            this.lbAge.TabIndex = 13;
            this.lbAge.Text = "年    龄:";
            // 
            // lbGender
            // 
            this.lbGender.AutoSize = true;
            this.lbGender.Font = new System.Drawing.Font("宋体", 12F);
            this.lbGender.Location = new System.Drawing.Point(39, 636);
            this.lbGender.Name = "lbGender";
            this.lbGender.Size = new System.Drawing.Size(118, 24);
            this.lbGender.TabIndex = 12;
            this.lbGender.Text = "性    别:";
            // 
            // tbUname
            // 
            this.tbUname.Font = new System.Drawing.Font("宋体", 12F);
            this.tbUname.Location = new System.Drawing.Point(199, 573);
            this.tbUname.Name = "tbUname";
            this.tbUname.Size = new System.Drawing.Size(286, 35);
            this.tbUname.TabIndex = 11;
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Font = new System.Drawing.Font("宋体", 12F);
            this.label6.Location = new System.Drawing.Point(39, 578);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(118, 24);
            this.label6.TabIndex = 2;
            this.label6.Text = "姓    名:";
            // 
            // tbReceive
            // 
            this.tbReceive.Location = new System.Drawing.Point(16, 35);
            this.tbReceive.Multiline = true;
            this.tbReceive.Name = "tbReceive";
            this.tbReceive.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.tbReceive.Size = new System.Drawing.Size(618, 520);
            this.tbReceive.TabIndex = 0;
            // 
            // OpenBrainViewer
            // 
            this.OpenBrainViewer.Font = new System.Drawing.Font("宋体", 12F);
            this.OpenBrainViewer.Location = new System.Drawing.Point(178, 197);
            this.OpenBrainViewer.Name = "OpenBrainViewer";
            this.OpenBrainViewer.Size = new System.Drawing.Size(131, 39);
            this.OpenBrainViewer.TabIndex = 6;
            this.OpenBrainViewer.Text = "脑部图像";
            this.OpenBrainViewer.UseVisualStyleBackColor = true;
            this.OpenBrainViewer.Click += new System.EventHandler(this.OpenBrainViewer_Click);
            // 
            // Software
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 18F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1063, 938);
            this.Controls.Add(this.gbReceive);
            this.Controls.Add(this.panel2);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.btnSearchPort);
            this.Controls.Add(this.panel1);
            this.Name = "Software";
            this.Text = "Software";
            this.Load += new System.EventHandler(this.Software_Load);
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.panel2.ResumeLayout(false);
            this.panel2.PerformLayout();
            this.gbReceive.ResumeLayout(false);
            this.gbReceive.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Button btnSearchPort;
        private System.Windows.Forms.ComboBox cbStop;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.ComboBox cbParity;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.ComboBox cbData;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.ComboBox cbBaud;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.ComboBox cbPort;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.GroupBox groupBox1;
        public System.Windows.Forms.CheckBox cb8;
        public System.Windows.Forms.CheckBox cb6;
        public System.Windows.Forms.CheckBox cb7;
        public System.Windows.Forms.CheckBox cb5;
        public System.Windows.Forms.CheckBox cb4;
        public System.Windows.Forms.CheckBox cb2;
        public System.Windows.Forms.CheckBox cb3;
        public System.Windows.Forms.CheckBox cb1;
        private System.Windows.Forms.Panel panel2;
        private System.Windows.Forms.Button btnOpen;
        private System.Windows.Forms.RadioButton rb16;
        private System.Windows.Forms.RadioButton rbString;
        private System.Windows.Forms.Button btnSavedata;
        private System.Windows.Forms.Button btnCollect;
        private System.Windows.Forms.GroupBox gbReceive;
        private System.Windows.Forms.TextBox tbReceive;
        private System.Windows.Forms.Button btnShowImage;
        private System.Windows.Forms.Button btnClear;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.TextBox tbAge;
        private System.Windows.Forms.TextBox tbGender;
        private System.Windows.Forms.Label lbAge;
        private System.Windows.Forms.Label lbGender;
        private System.Windows.Forms.TextBox tbUname;
        private System.Windows.Forms.Button OpenBrainViewer;
    }
}