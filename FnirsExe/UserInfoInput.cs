using MySql.Data.MySqlClient;
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
    public partial class UserInfoInput : Form
    {
        public UserInfoInput()
        {
            InitializeComponent();
        }
        /// <summary>
        /// 数据库连接字符串
        /// </summary>
        string conStr = "Data Source=fnirsexe.db;Version=3;";
        public event Action DataSaved;
        private void label1_Click(object sender, EventArgs e)
        {

        }

        private void label2_Click(object sender, EventArgs e)
        {

        }

        private void label9_Click(object sender, EventArgs e)
        {

        }

        private void textBox3_TextChanged(object sender, EventArgs e)
        {

        }

        private void textBox2_TextChanged(object sender, EventArgs e)
        {

        }
        /// <summary>
        /// 保存按钮单击事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnSave_Click(object sender, EventArgs e)
        {
            /// <summary>
            /// 新增信息SQL语句
            /// </summary>
            string opstr = $"insert into user_tests(name,age,gender,test_date,test_duration," +
                             $"test_part,test_path,remarks) values('{tbUname.Text}'," +
                            $"{int.Parse(tbUage.Text)},'{tbUgender.Text}','{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}'," +
                            $"{int.Parse(tbUtime.Text)},'{tbUpart.Text}','{tbUpath.Text}','{tbUremarks.Text}');";
            SQLiteOpHelper msoh = new SQLiteOpHelper(conStr);
            msoh.OpAddDeleteUpdateMySql(opstr);
            //输入完之后，几个文本框设置为空字符串
            tbUname.Text = "";
            tbUage.Text = "";
            tbUgender.Text = "";
            tbUdate.Text = "";
            tbUtime.Text = "";
            tbUpart.Text = "";
            tbUpath.Text = "";
            tbUremarks.Text = "";
            DataSaved?.Invoke();
            this.Close();
        }

        private void label4_Click(object sender, EventArgs e)
        {

        }
    }
}
