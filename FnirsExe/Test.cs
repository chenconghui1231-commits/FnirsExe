using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Deployment.Application;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;
using static System.Net.Mime.MediaTypeNames;

namespace FnirsExe
{
    public partial class Test : Form
    {
        private Test test;
        public Test()
        {
            InitializeComponent();
            this.Load += Test_Load;
        }
        /// <summary>
        /// 数据库连接字符串
        /// </summary>
        string conStr = "Data Source=fnirsexe.db;Version=3;";
        string queryStr = "select id,name,age,gender,test_date,test_duration," +
                             "test_part,test_path,remarks from user_tests;";

        SQLiteOpHelper msoh = null;
        /// <summary>
        /// 
        /// 新增单击事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button1_Click(object sender, EventArgs e)
        {
            UserInfoInput userInfoInput = new UserInfoInput();
            userInfoInput.DataSaved += RefreshDataGridView;
            userInfoInput.Show();
        }

        private void label1_Click(object sender, EventArgs e)
        {

        }
        /// <summary>
        /// 修改信息事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button3_Click(object sender, EventArgs e)
        {
            UpdateInfo();
        }
        /// <summary>
        /// 修改信息方法
        /// </summary>
        private void UpdateInfo()
        {
            try
            {
                // 1. 检查是否选中行
                if (dgvShow.CurrentRow == null)
                {
                    MessageBox.Show("请先选择要修改的行！");
                    return;
                }

                // 2. 获取选中用户
                User currentSelectedUser = msoh.stus[dgvShow.CurrentRow.Index];

                // 3. 格式化日期（修正格式）
                string testDateValue = Convert.ToDateTime(dgvShow["test_date", dgvShow.CurrentRow.Index].Value)
                      .ToString("yyyy-MM-dd HH:mm:ss");

                // 4. 正确拼接SQL语句
                string updateStr = $@"UPDATE user_tests SET 
                            name = '{dgvShow["name", dgvShow.CurrentRow.Index].Value?.ToString()?.Replace("'", "''")}', 
                            age = {dgvShow["age", dgvShow.CurrentRow.Index].Value},
                            gender = '{dgvShow["gender", dgvShow.CurrentRow.Index].Value?.ToString()?.Replace("'", "''")}',
                            test_date = '{testDateValue}',
                            test_duration = {dgvShow["test_duration", dgvShow.CurrentRow.Index].Value},
                            test_part = '{dgvShow["test_part", dgvShow.CurrentRow.Index].Value?.ToString()?.Replace("'", "''")}',
                            test_path = '{dgvShow["test_path", dgvShow.CurrentRow.Index].Value?.ToString()?.Replace("'", "''")}',
                            remarks = '{dgvShow["remarks", dgvShow.CurrentRow.Index].Value?.ToString()?.Replace("'", "''")}'
                            WHERE id = {currentSelectedUser.id}";

                // 5. 执行更新
                msoh = new SQLiteOpHelper(conStr);
                msoh.OpAddDeleteUpdateMySql(updateStr);

                MessageBox.Show("修改成功！");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"修改失败: {ex.Message}");
            }
        }

        private void buttonClick(object sender, EventArgs e)
        {
            // 按钮点击事件处理
        }

        private void button6_Click(object sender, EventArgs e)
        {
            //new MainForm().Show();
            this.Close();
        }
        /// <summary>
        /// 查询事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button5_Click(object sender, EventArgs e)
        {
            if(tbSelected.Text == "")
            {
                QueryInfo();
            }
            else
            {
                queryStr = $"select * from user_tests where name='{tbSelected.Text}' or id='{tbSelected.Text}';";
                QueryInfo();
                queryStr = "select id,name,age,gender,test_date,test_duration," +
                            "test_part,test_path,remarks from user_tests;";
            }
        }
        /// <summary>
        /// 查询方法
        /// </summary>
        private void QueryInfo()
        {
                msoh = new SQLiteOpHelper(conStr);
                msoh.OpQueryMySql(queryStr);
                //将dategridview的数据源设置为msop对象的一个List数据成员stus
                dgvShow.DataSource = msoh.stus; 
        }

        private void tbSelected_TextChanged(object sender, EventArgs e)
        {

        }
        /// <summary>
        /// 删除按钮单击事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnDelete_Click(object sender, EventArgs e)
        {
            DeleteInfo(); // 执行删除
            RefreshDataGridView(); // 删除后刷新 DataGridView
        }

        private void DeleteInfo()
        {
            //获取选择行的User对象
            //删除信息的SQL语句
            User currentSelectdUser = msoh.stus[dgvShow.CurrentRow.Index];
            string deleteStr = $"delete from user_tests where id={currentSelectdUser.id}";
            msoh = new SQLiteOpHelper(conStr);
            msoh.OpAddDeleteUpdateMySql(deleteStr);
            ReorganizeIds();
        }
        private void ReorganizeIds()
        {
            string updateIdsSql = @"
                    -- 创建临时表备份数据
                    CREATE TEMPORARY TABLE temp_user AS 
                    SELECT name, age, gender, test_date, test_duration, test_part, test_path, remarks 
                    FROM user_tests ORDER BY id;

                    -- 删除原表所有数据
                    DELETE FROM user_tests;

                    -- 重置自增计数器（重要！）
                    DELETE FROM sqlite_sequence WHERE name='user_tests';

                    -- 重新插入数据（ID将从1开始重新分配）
                    INSERT INTO user_tests (name, age, gender, test_date, test_duration, test_part, test_path, remarks)
                    SELECT name, age, gender, test_date, test_duration, test_part, test_path, remarks FROM temp_user;

                    -- 删除临时表
                    DROP TABLE temp_user;
                ";
            msoh.OpAddDeleteUpdateMySql(updateIdsSql);
        }
        // 刷新 DataGridView 数据
        private void RefreshDataGridView()
        {
            string query = "SELECT * FROM user_tests"; // 查询最新数据
            DataTable dt = new SQLiteOpHelper(conStr).GetDataTable(query);
            dgvShow.DataSource = dt; // 重新绑定数据源
        }

        private void dgvShow_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            RefreshDataGridView();
        }
        /// <summary>
        /// 取消按钮单击事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public event Action OperationCancelled;
        private void btnCancel_Click(object sender, EventArgs e)
        {
            try
            {
                tbSelected.Text = "";
                dgvShow.ClearSelection();

                // 触发取消事件
                OperationCancelled?.Invoke();

                tbSelected.Focus();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"取消操作时出错: {ex.Message}", "错误",
                               MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void Test_Load(object sender, EventArgs e)
        {
            RefreshDataGridView();
        }
       
    }
}
