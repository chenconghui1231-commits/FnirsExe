namespace FnirsExe
{
    partial class BrainViewerForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

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
            this.glControlHbO = new OpenTK.GLControl();
            this.glControlHbR = new OpenTK.GLControl();
            this.glControlHbT = new OpenTK.GLControl();
            this.lblTitleHbO = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.colorBarPanel = new System.Windows.Forms.Panel();
            this.colorBarLabelMax = new System.Windows.Forms.Label();
            this.colorBarLabelMidHigh = new System.Windows.Forms.Label();
            this.colorBarLabelMid = new System.Windows.Forms.Label();
            this.colorBarLabelMidLow = new System.Windows.Forms.Label();
            this.colorBarLabelMin = new System.Windows.Forms.Label();
            this.colorBarTitle = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // glControlHbO
            // 
            this.glControlHbO.BackColor = System.Drawing.SystemColors.ControlLight;
            this.glControlHbO.ForeColor = System.Drawing.SystemColors.Control;
            this.glControlHbO.Location = new System.Drawing.Point(70, 131);
            this.glControlHbO.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.glControlHbO.Name = "glControlHbO";
            this.glControlHbO.Size = new System.Drawing.Size(430, 522);
            this.glControlHbO.TabIndex = 27;
            this.glControlHbO.VSync = false;
            // 
            // glControlHbR
            // 
            this.glControlHbR.BackColor = System.Drawing.SystemColors.ControlLight;
            this.glControlHbR.ForeColor = System.Drawing.SystemColors.Control;
            this.glControlHbR.Location = new System.Drawing.Point(562, 131);
            this.glControlHbR.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.glControlHbR.Name = "glControlHbR";
            this.glControlHbR.Size = new System.Drawing.Size(430, 522);
            this.glControlHbR.TabIndex = 28;
            this.glControlHbR.VSync = false;
            // 
            // glControlHbT
            // 
            this.glControlHbT.BackColor = System.Drawing.SystemColors.ControlLight;
            this.glControlHbT.ForeColor = System.Drawing.SystemColors.Control;
            this.glControlHbT.Location = new System.Drawing.Point(1044, 131);
            this.glControlHbT.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.glControlHbT.Name = "glControlHbT";
            this.glControlHbT.Size = new System.Drawing.Size(430, 522);
            this.glControlHbT.TabIndex = 29;
            this.glControlHbT.VSync = false;
            // 
            // lblTitleHbO
            // 
            this.lblTitleHbO.AutoSize = true;
            this.lblTitleHbO.Font = new System.Drawing.Font("宋体", 12F);
            this.lblTitleHbO.ForeColor = System.Drawing.Color.Red;
            this.lblTitleHbO.Location = new System.Drawing.Point(246, 71);
            this.lblTitleHbO.Name = "lblTitleHbO";
            this.lblTitleHbO.Size = new System.Drawing.Size(46, 24);
            this.lblTitleHbO.TabIndex = 30;
            this.lblTitleHbO.Text = "HbO";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("宋体", 12F);
            this.label1.ForeColor = System.Drawing.Color.Green;
            this.label1.Location = new System.Drawing.Point(723, 71);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(46, 24);
            this.label1.TabIndex = 31;
            this.label1.Text = "HbR";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Font = new System.Drawing.Font("宋体", 12F);
            this.label2.ForeColor = System.Drawing.Color.Blue;
            this.label2.Location = new System.Drawing.Point(1223, 71);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(46, 24);
            this.label2.TabIndex = 32;
            this.label2.Text = "HbT";
            // 
            // colorBarPanel
            // 
            this.colorBarPanel.BackColor = System.Drawing.SystemColors.Control;
            this.colorBarPanel.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.colorBarPanel.Location = new System.Drawing.Point(1556, 131);
            this.colorBarPanel.Name = "colorBarPanel";
            this.colorBarPanel.Size = new System.Drawing.Size(44, 522);
            this.colorBarPanel.TabIndex = 33;
            this.colorBarPanel.Paint += new System.Windows.Forms.PaintEventHandler(this.colorBarPanel_Paint);
            // 
            // colorBarLabelMax
            // 
            this.colorBarLabelMax.AutoSize = true;
            this.colorBarLabelMax.BackColor = System.Drawing.Color.Transparent;
            this.colorBarLabelMax.Font = new System.Drawing.Font("宋体", 8F);
            this.colorBarLabelMax.Location = new System.Drawing.Point(1620, 134);
            this.colorBarLabelMax.Name = "colorBarLabelMax";
            this.colorBarLabelMax.Size = new System.Drawing.Size(31, 16);
            this.colorBarLabelMax.TabIndex = 5;
            this.colorBarLabelMax.Text = "2.6";
            // 
            // colorBarLabelMidHigh
            // 
            this.colorBarLabelMidHigh.AutoSize = true;
            this.colorBarLabelMidHigh.BackColor = System.Drawing.Color.Transparent;
            this.colorBarLabelMidHigh.Font = new System.Drawing.Font("宋体", 8F);
            this.colorBarLabelMidHigh.Location = new System.Drawing.Point(1620, 266);
            this.colorBarLabelMidHigh.Name = "colorBarLabelMidHigh";
            this.colorBarLabelMidHigh.Size = new System.Drawing.Size(31, 16);
            this.colorBarLabelMidHigh.TabIndex = 4;
            this.colorBarLabelMidHigh.Text = "1.3";
            // 
            // colorBarLabelMid
            // 
            this.colorBarLabelMid.AutoSize = true;
            this.colorBarLabelMid.BackColor = System.Drawing.Color.Transparent;
            this.colorBarLabelMid.Font = new System.Drawing.Font("宋体", 8F);
            this.colorBarLabelMid.Location = new System.Drawing.Point(1620, 391);
            this.colorBarLabelMid.Name = "colorBarLabelMid";
            this.colorBarLabelMid.Size = new System.Drawing.Size(31, 16);
            this.colorBarLabelMid.TabIndex = 3;
            this.colorBarLabelMid.Text = "0.0";
            // 
            // colorBarLabelMidLow
            // 
            this.colorBarLabelMidLow.AutoSize = true;
            this.colorBarLabelMidLow.BackColor = System.Drawing.Color.Transparent;
            this.colorBarLabelMidLow.Font = new System.Drawing.Font("宋体", 8F);
            this.colorBarLabelMidLow.Location = new System.Drawing.Point(1620, 523);
            this.colorBarLabelMidLow.Name = "colorBarLabelMidLow";
            this.colorBarLabelMidLow.Size = new System.Drawing.Size(39, 16);
            this.colorBarLabelMidLow.TabIndex = 2;
            this.colorBarLabelMidLow.Text = "-1.3";
            // 
            // colorBarLabelMin
            // 
            this.colorBarLabelMin.AutoSize = true;
            this.colorBarLabelMin.BackColor = System.Drawing.Color.Transparent;
            this.colorBarLabelMin.Font = new System.Drawing.Font("宋体", 8F);
            this.colorBarLabelMin.Location = new System.Drawing.Point(1620, 637);
            this.colorBarLabelMin.Name = "colorBarLabelMin";
            this.colorBarLabelMin.Size = new System.Drawing.Size(39, 16);
            this.colorBarLabelMin.TabIndex = 1;
            this.colorBarLabelMin.Text = "-2.5";
            // 
            // colorBarTitle
            // 
            this.colorBarTitle.AutoSize = true;
            this.colorBarTitle.BackColor = System.Drawing.Color.Transparent;
            this.colorBarTitle.Font = new System.Drawing.Font("宋体", 9F);
            this.colorBarTitle.Location = new System.Drawing.Point(1542, 91);
            this.colorBarTitle.Name = "colorBarTitle";
            this.colorBarTitle.Size = new System.Drawing.Size(44, 18);
            this.colorBarTitle.TabIndex = 0;
            this.colorBarTitle.Text = "浓度";
            // 
            // BrainViewerForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 18F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(2368, 1027);
            this.Controls.Add(this.colorBarTitle);
            this.Controls.Add(this.colorBarLabelMax);
            this.Controls.Add(this.colorBarPanel);
            this.Controls.Add(this.colorBarLabelMidHigh);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.colorBarLabelMin);
            this.Controls.Add(this.colorBarLabelMidLow);
            this.Controls.Add(this.colorBarLabelMid);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.lblTitleHbO);
            this.Controls.Add(this.glControlHbT);
            this.Controls.Add(this.glControlHbR);
            this.Controls.Add(this.glControlHbO);
            this.Name = "BrainViewerForm";
            this.Text = "BrainViewerForm";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private OpenTK.GLControl glControlHbO;
        private OpenTK.GLControl glControlHbR;
        private OpenTK.GLControl glControlHbT;
        private System.Windows.Forms.Label lblTitleHbO;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Panel colorBarPanel;
        private System.Windows.Forms.Label colorBarLabelMax;
        private System.Windows.Forms.Label colorBarLabelMidHigh;
        private System.Windows.Forms.Label colorBarLabelMid;
        private System.Windows.Forms.Label colorBarLabelMidLow;
        private System.Windows.Forms.Label colorBarLabelMin;
        private System.Windows.Forms.Label colorBarTitle;
    }
}