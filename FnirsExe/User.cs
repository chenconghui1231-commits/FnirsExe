using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FnirsExe
{
    public class User
    {
        public int id { get; set; }
        public string name { get; set; }
        public int age { get; set; }
        public string gender { get; set; }
        public DateTime test_date { get; set; }
        public int test_duration { get; set; }
        public string test_part { get; set; }
        public string test_path { get; set; }
        public string remarks { get; set; }

        public User(int id, string name, int age, string gender, DateTime test_date, int test_duration, string test_part, string test_path, string remarks)
        {
            this.id = id;
            this.name = name ?? throw new ArgumentNullException(nameof(name));
            this.age = age;
            this.gender = gender ?? throw new ArgumentNullException(nameof(gender));
            this.test_date = test_date;
            this.test_duration = test_duration;
            this.test_part = test_part ?? throw new ArgumentNullException(nameof(test_part));
            this.test_path = test_path ?? throw new ArgumentNullException(nameof(test_path));
            this.remarks = remarks ?? throw new ArgumentNullException(nameof(remarks));
        }

        public override string ToString()
        {
            return "被试信息：" + "被试ID:" + id.ToString() +
                "姓名:" + name + "年龄:" + age.ToString() +
                "性别:" + gender + "测试日期:" + test_date.ToString("yyyy-MM-dd") +
                "测试时长:" + test_duration.ToString() + "测试部位:" + test_part +
                "文件路径:" + test_path + "备注:" + remarks;
        }
    }
}
