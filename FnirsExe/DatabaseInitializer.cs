using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FnirsExe
{
    public static class DatabaseInitializer
    {
        public static void InitializeDatabase(string connectionString)
        {
            try
            {
                using (var conn = new SQLiteConnection(connectionString))
                {
                    conn.Open();

                    string createTableSql = @"
                    CREATE TABLE IF NOT EXISTS user_tests (
                        id INTEGER PRIMARY KEY AUTOINCREMENT,
                        name TEXT NOT NULL,
                        age INTEGER NOT NULL,
                        gender TEXT NOT NULL,
                        test_date TEXT NOT NULL,
                        test_duration INTEGER NOT NULL,
                        test_part TEXT NOT NULL,
                        test_path TEXT NOT NULL,
                        remarks TEXT
                    )";

                    using (var cmd = new SQLiteCommand(createTableSql, conn))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"数据库初始化失败: {ex.Message}", "错误",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
