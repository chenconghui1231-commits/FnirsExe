using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FnirsExe
{
    public partial class MainForm : Form
    {
        private Test test;
        private Software software;
        private Image image;
        public MainForm()
        {
            InitializeComponent();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {

        }

        private void btnTest_Click(object sender, EventArgs e)
        {
            if (test == null || test.IsDisposed)
            {
                test = new Test();
                test.FormClosed += (s, args) => test = null; // 释放后重置引用
                test.Show();
            }
            else
            {
                test.BringToFront(); // 如果已打开，则聚焦到该窗体
            }
        }
        private void btnSoftware_Click(object sender, EventArgs e)
        {
            if (software == null || software.IsDisposed)
            {
                software = new Software();
                software.FormClosed += (s, args) => software = null;
                software.Show();
                //this.Hide();
            }
            else
            {
                software.BringToFront();
            }
        }


        private void btnImage_Click(object sender, EventArgs e)
        {
            if (image == null || image.IsDisposed)
            {
                image = new Image();
                image.FormClosed += (s, args) => image = null; // 释放后重置引用
                image.Show();
            }
            else
            {
                image.BringToFront(); // 如果已打开，则聚焦到该窗体
            }
        }
    }
}
