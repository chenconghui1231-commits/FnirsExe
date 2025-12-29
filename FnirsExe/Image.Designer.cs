namespace FnirsExe
{
    partial class Image
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
            this.fpImage1 = new ScottPlot.WinForms.FormsPlot();
            this.btnLoadData = new System.Windows.Forms.Button();
            this.fpImage2 = new ScottPlot.WinForms.FormsPlot();
            this.fpImage3 = new ScottPlot.WinForms.FormsPlot();
            this.fpImage5 = new ScottPlot.WinForms.FormsPlot();
            this.fpImage4 = new ScottPlot.WinForms.FormsPlot();
            this.fpImage6 = new ScottPlot.WinForms.FormsPlot();
            this.fpImage7 = new ScottPlot.WinForms.FormsPlot();
            this.fpImage8 = new ScottPlot.WinForms.FormsPlot();
            this.cbHbO = new System.Windows.Forms.CheckBox();
            this.cbHbT = new System.Windows.Forms.CheckBox();
            this.cbHbR = new System.Windows.Forms.CheckBox();
            this.btnInspectSnirf = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // fpImage1
            // 
            this.fpImage1.DisplayScale = 0F;
            this.fpImage1.Location = new System.Drawing.Point(12, 12);
            this.fpImage1.Name = "fpImage1";
            this.fpImage1.Size = new System.Drawing.Size(558, 283);
            this.fpImage1.TabIndex = 0;
            this.fpImage1.Load += new System.EventHandler(this.fpImage1_Load);
            // 
            // btnLoadData
            // 
            this.btnLoadData.Font = new System.Drawing.Font("宋体", 12F);
            this.btnLoadData.Location = new System.Drawing.Point(738, 687);
            this.btnLoadData.Name = "btnLoadData";
            this.btnLoadData.Size = new System.Drawing.Size(170, 52);
            this.btnLoadData.TabIndex = 1;
            this.btnLoadData.Text = "载入数据";
            this.btnLoadData.UseVisualStyleBackColor = true;
            this.btnLoadData.Click += new System.EventHandler(this.btnLoadData_Click);
            // 
            // fpImage2
            // 
            this.fpImage2.DisplayScale = 0F;
            this.fpImage2.Location = new System.Drawing.Point(610, 12);
            this.fpImage2.Name = "fpImage2";
            this.fpImage2.Size = new System.Drawing.Size(558, 283);
            this.fpImage2.TabIndex = 8;
            this.fpImage2.Load += new System.EventHandler(this.fpImage2_Load);
            // 
            // fpImage3
            // 
            this.fpImage3.DisplayScale = 0F;
            this.fpImage3.Location = new System.Drawing.Point(1208, 12);
            this.fpImage3.Name = "fpImage3";
            this.fpImage3.Size = new System.Drawing.Size(558, 283);
            this.fpImage3.TabIndex = 9;
            // 
            // fpImage5
            // 
            this.fpImage5.DisplayScale = 0F;
            this.fpImage5.Location = new System.Drawing.Point(12, 357);
            this.fpImage5.Name = "fpImage5";
            this.fpImage5.Size = new System.Drawing.Size(558, 282);
            this.fpImage5.TabIndex = 10;
            // 
            // fpImage4
            // 
            this.fpImage4.DisplayScale = 0F;
            this.fpImage4.Location = new System.Drawing.Point(1811, 12);
            this.fpImage4.Name = "fpImage4";
            this.fpImage4.Size = new System.Drawing.Size(558, 283);
            this.fpImage4.TabIndex = 11;
            this.fpImage4.Load += new System.EventHandler(this.fpImage4_Load);
            // 
            // fpImage6
            // 
            this.fpImage6.DisplayScale = 0F;
            this.fpImage6.Location = new System.Drawing.Point(610, 357);
            this.fpImage6.Name = "fpImage6";
            this.fpImage6.Size = new System.Drawing.Size(558, 282);
            this.fpImage6.TabIndex = 12;
            // 
            // fpImage7
            // 
            this.fpImage7.DisplayScale = 0F;
            this.fpImage7.Location = new System.Drawing.Point(1208, 357);
            this.fpImage7.Name = "fpImage7";
            this.fpImage7.Size = new System.Drawing.Size(558, 282);
            this.fpImage7.TabIndex = 13;
            // 
            // fpImage8
            // 
            this.fpImage8.DisplayScale = 0F;
            this.fpImage8.Location = new System.Drawing.Point(1811, 357);
            this.fpImage8.Name = "fpImage8";
            this.fpImage8.Size = new System.Drawing.Size(558, 282);
            this.fpImage8.TabIndex = 14;
            // 
            // cbHbO
            // 
            this.cbHbO.AutoSize = true;
            this.cbHbO.Font = new System.Drawing.Font("宋体", 12F);
            this.cbHbO.ForeColor = System.Drawing.Color.Red;
            this.cbHbO.Location = new System.Drawing.Point(63, 700);
            this.cbHbO.Margin = new System.Windows.Forms.Padding(4);
            this.cbHbO.Name = "cbHbO";
            this.cbHbO.Size = new System.Drawing.Size(72, 28);
            this.cbHbO.TabIndex = 23;
            this.cbHbO.Text = "HbO";
            this.cbHbO.UseVisualStyleBackColor = true;
            this.cbHbO.CheckedChanged += new System.EventHandler(this.cbHbO_CheckedChanged);
            // 
            // cbHbT
            // 
            this.cbHbT.AutoSize = true;
            this.cbHbT.Font = new System.Drawing.Font("宋体", 12F);
            this.cbHbT.ForeColor = System.Drawing.Color.Blue;
            this.cbHbT.Location = new System.Drawing.Point(275, 700);
            this.cbHbT.Margin = new System.Windows.Forms.Padding(4);
            this.cbHbT.Name = "cbHbT";
            this.cbHbT.Size = new System.Drawing.Size(72, 28);
            this.cbHbT.TabIndex = 24;
            this.cbHbT.Text = "HbT";
            this.cbHbT.UseVisualStyleBackColor = true;
            this.cbHbT.CheckedChanged += new System.EventHandler(this.cbHbT_CheckedChanged);
            // 
            // cbHbR
            // 
            this.cbHbR.AutoSize = true;
            this.cbHbR.Font = new System.Drawing.Font("宋体", 12F);
            this.cbHbR.ForeColor = System.Drawing.Color.ForestGreen;
            this.cbHbR.Location = new System.Drawing.Point(166, 700);
            this.cbHbR.Margin = new System.Windows.Forms.Padding(4);
            this.cbHbR.Name = "cbHbR";
            this.cbHbR.Size = new System.Drawing.Size(72, 28);
            this.cbHbR.TabIndex = 25;
            this.cbHbR.Text = "HbR";
            this.cbHbR.UseVisualStyleBackColor = true;
            this.cbHbR.CheckedChanged += new System.EventHandler(this.cbHbR_CheckedChanged);
            // 
            // btnInspectSnirf
            // 
            this.btnInspectSnirf.Font = new System.Drawing.Font("宋体", 12F);
            this.btnInspectSnirf.Location = new System.Drawing.Point(1108, 687);
            this.btnInspectSnirf.Name = "btnInspectSnirf";
            this.btnInspectSnirf.Size = new System.Drawing.Size(170, 52);
            this.btnInspectSnirf.TabIndex = 26;
            this.btnInspectSnirf.Text = "查看snirf";
            this.btnInspectSnirf.UseVisualStyleBackColor = true;
            this.btnInspectSnirf.Click += new System.EventHandler(this.btnInspectSnirf_Click);
            // 
            // Image
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 18F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(2564, 1200);
            this.Controls.Add(this.btnInspectSnirf);
            this.Controls.Add(this.cbHbR);
            this.Controls.Add(this.cbHbT);
            this.Controls.Add(this.cbHbO);
            this.Controls.Add(this.fpImage8);
            this.Controls.Add(this.fpImage7);
            this.Controls.Add(this.fpImage6);
            this.Controls.Add(this.fpImage4);
            this.Controls.Add(this.fpImage5);
            this.Controls.Add(this.fpImage3);
            this.Controls.Add(this.fpImage2);
            this.Controls.Add(this.btnLoadData);
            this.Controls.Add(this.fpImage1);
            this.Name = "Image";
            this.Text = "Image";
            this.Load += new System.EventHandler(this.Image_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private ScottPlot.WinForms.FormsPlot fpImage1;
        private System.Windows.Forms.Button btnLoadData;
        private ScottPlot.WinForms.FormsPlot fpImage2;
        private ScottPlot.WinForms.FormsPlot fpImage3;
        private ScottPlot.WinForms.FormsPlot fpImage5;
        private ScottPlot.WinForms.FormsPlot fpImage4;
        private ScottPlot.WinForms.FormsPlot fpImage6;
        private ScottPlot.WinForms.FormsPlot fpImage7;
        private ScottPlot.WinForms.FormsPlot fpImage8;
        private System.Windows.Forms.CheckBox cbHbO;
        private System.Windows.Forms.CheckBox cbHbT;
        private System.Windows.Forms.CheckBox cbHbR;
        private System.Windows.Forms.Button btnInspectSnirf;
    }
}