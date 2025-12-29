namespace FnirsExe
{
    partial class MainForm
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
            this.btnTest = new System.Windows.Forms.Button();
            this.btnSoftware = new System.Windows.Forms.Button();
            this.btnImage = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // btnTest
            // 
            this.btnTest.Font = new System.Drawing.Font("宋体", 12F);
            this.btnTest.Location = new System.Drawing.Point(253, 77);
            this.btnTest.Name = "btnTest";
            this.btnTest.Size = new System.Drawing.Size(263, 60);
            this.btnTest.TabIndex = 0;
            this.btnTest.Text = "被试信息录入功能";
            this.btnTest.UseVisualStyleBackColor = true;
            this.btnTest.Click += new System.EventHandler(this.btnTest_Click);
            // 
            // btnSoftware
            // 
            this.btnSoftware.Font = new System.Drawing.Font("宋体", 12F);
            this.btnSoftware.Location = new System.Drawing.Point(253, 182);
            this.btnSoftware.Name = "btnSoftware";
            this.btnSoftware.Size = new System.Drawing.Size(263, 66);
            this.btnSoftware.TabIndex = 1;
            this.btnSoftware.Text = "软件控制功能";
            this.btnSoftware.UseVisualStyleBackColor = true;
            this.btnSoftware.Click += new System.EventHandler(this.btnSoftware_Click);
            // 
            // btnImage
            // 
            this.btnImage.Font = new System.Drawing.Font("宋体", 12F);
            this.btnImage.Location = new System.Drawing.Point(253, 289);
            this.btnImage.Name = "btnImage";
            this.btnImage.Size = new System.Drawing.Size(263, 65);
            this.btnImage.TabIndex = 2;
            this.btnImage.Text = "图形显示功能";
            this.btnImage.UseVisualStyleBackColor = true;
            this.btnImage.Click += new System.EventHandler(this.btnImage_Click);
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 18F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Controls.Add(this.btnImage);
            this.Controls.Add(this.btnSoftware);
            this.Controls.Add(this.btnTest);
            this.Name = "MainForm";
            this.Text = "MainForm";
            this.Load += new System.EventHandler(this.MainForm_Load);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button btnTest;
        private System.Windows.Forms.Button btnSoftware;
        private System.Windows.Forms.Button btnImage;
    }
}