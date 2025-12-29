using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SQLite;
using System.Windows.Forms;
using FnirsExe;
using System.Data;

internal class SQLiteOpHelper{
    private string conStr = null;
    private SQLiteConnection sqliteConn = null;
    private SQLiteCommand sqliteCmd = null;
    private SQLiteDataReader sqliteReader = null;
    //用于系统登录 存储用户名和密码
    public Dictionary<string, string> dics = null;
    //用于数据库查询后的数据对象List<UserInfo>
    public List<User> stus = null;
    public SQLiteOpHelper(string conStr)
    {
        this.conStr = conStr ?? throw new ArgumentNullException(nameof(conStr));
        InitializeDatabase();
    }
    //初始化数据库和数据表
    private void InitializeDatabase()
    {
        try
        {
            // 如果数据库文件不存在，SQLite会自动创建
            using (var conn = new SQLiteConnection(conStr))
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
    public void OpLoginMySql(string queryStr)
    {
        try
        {
            sqliteConn = new SQLiteConnection(conStr);
            sqliteConn.Open();
            sqliteCmd = new SQLiteCommand(queryStr, this.sqliteConn);
            sqliteReader = sqliteCmd.ExecuteReader();
            dics = new Dictionary<string, string>();
            while (sqliteReader.Read())
            {
                dics.Add(sqliteReader[0].ToString(), sqliteReader[1].ToString());
            }
        }
        catch
        {
            MessageBox.Show("登陆失败！", "用户登录", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            sqliteCmd.Dispose();
            sqliteConn.Close();
        }
    }
    ///数据库增删改
    public void OpAddDeleteUpdateMySql(string opStr)
    {
        try
        {
            sqliteConn = new SQLiteConnection(conStr);
            sqliteCmd = new SQLiteCommand(opStr, this.sqliteConn);
            sqliteConn.Open();
            sqliteCmd.ExecuteNonQuery();
            MessageBox.Show("操作成功！", "数据库操作", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch
        {
            MessageBox.Show("操作失败！", "数据库操作", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            sqliteCmd?.Dispose();
            sqliteConn?.Close();
        }
    }
    public void OpQueryMySql(string queryStr)
    {
        try
        {
            sqliteConn = new SQLiteConnection(conStr);
            sqliteCmd = new SQLiteCommand(queryStr, this.sqliteConn);
            sqliteConn.Open();
            sqliteReader = sqliteCmd.ExecuteReader();
            stus = new List<User>();
            while (sqliteReader.Read())
            {
                stus.Add(new User(
                    int.Parse(sqliteReader[0].ToString()),
                    sqliteReader[1].ToString(),
                    int.Parse(sqliteReader[2].ToString()),
                    sqliteReader[3].ToString(),
                    DateTime.Parse(sqliteReader[4].ToString()),
                    int.Parse(sqliteReader[5].ToString()),
                    sqliteReader[6].ToString(),
                    sqliteReader[7].ToString(),
                    sqliteReader[8].ToString()
                ));
            }
        }
        catch
        {
            MessageBox.Show("查询失败！", "数据库查询", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            sqliteCmd?.Dispose();
            sqliteConn?.Close();
        }
    }
    // 获取 DataTable 的方法
    public DataTable GetDataTable(string query)
    {
        DataTable dataTable = new DataTable();

        try
        {
            using (sqliteConn = new SQLiteConnection(conStr))
            {
                sqliteConn.Open();
                using (sqliteCmd = new SQLiteCommand(query, sqliteConn))
                {
                    using (SQLiteDataAdapter adapter = new SQLiteDataAdapter(sqliteCmd))
                    {
                        adapter.Fill(dataTable);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"查询失败: {ex.Message}", "数据库错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return null; // 或抛出异常 throw;
        }

        return dataTable;
    }
}
