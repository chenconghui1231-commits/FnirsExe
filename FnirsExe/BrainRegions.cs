using OpenTK;

namespace FnirsExe
{
    public static class BrainRegions
    {
        // 脑区定义
        public enum Region
        {//脑区枚举定义
            Unknown = 0,
            PrefrontalLeft,     // 前额叶左侧
            PrefrontalRight,    // 前额叶右侧
            MotorLeft,          // 运动皮层左侧
            MotorRight,         // 运动皮层右侧
            ParietalLeft,       // 顶叶左侧
            ParietalRight,      // 顶叶右侧
            TemporalLeft,       // 颞叶左侧
            TemporalRight,      // 颞叶右侧
            OccipitalLeft,      // 枕叶左侧
            OccipitalRight      // 枕叶右侧
        }

        // 脑区边界定义（简化版，基于MNI坐标）
        public static class RegionBounds
        {//使用MNI（蒙特利尔神经研究所）坐标系：
            //X轴：左右方向（左负右正）
            //Y轴：前后方向（前正后负）
            //Z轴：上下方向（上正下负）
            // 前额叶边界
            public static (float minX, float maxX, float minY, float maxY, float minZ, float maxZ) Prefrontal =
                (-60, 60, 30, 90, 20, 70);

            // 运动皮层边界
            public static (float minX, float maxX, float minY, float maxY, float minZ, float maxZ) Motor =
                (-70, 70, -20, 30, 40, 80);

            // 顶叶边界
            public static (float minX, float maxX, float minY, float maxY, float minZ, float maxZ) Parietal =
                (-60, 60, -70, -20, 30, 70);

            // 颞叶边界
            public static (float minX, float maxX, float minY, float maxY, float minZ, float maxZ) Temporal =
                (-80, -20, -40, 20, -20, 40); // 左侧
                                              // 右侧颞叶需要单独定义

            // 枕叶边界
            public static (float minX, float maxX, float minY, float maxY, float minZ, float maxZ) Occipital =
                (-50, 50, -100, -60, -10, 30);
        }

        /// <summary>
        /// 根据MNI坐标判断脑区
        /// </summary>
        public static Region GetRegionFromPosition(Vector3 position)
        {//提取3D坐标的各个分量。
            float x = position.X;
            float y = position.Y;
            float z = position.Z;

            // 前额叶
            if (x >= RegionBounds.Prefrontal.minX && x <= RegionBounds.Prefrontal.maxX &&
                y >= RegionBounds.Prefrontal.minY && y <= RegionBounds.Prefrontal.maxY &&
                z >= RegionBounds.Prefrontal.minZ && z <= RegionBounds.Prefrontal.maxZ)
            {
                return x < 0 ? Region.PrefrontalLeft : Region.PrefrontalRight;
            }

            // 运动皮层
            if (x >= RegionBounds.Motor.minX && x <= RegionBounds.Motor.maxX &&
                y >= RegionBounds.Motor.minY && y <= RegionBounds.Motor.maxY &&
                z >= RegionBounds.Motor.minZ && z <= RegionBounds.Motor.maxZ)
            {
                return x < 0 ? Region.MotorLeft : Region.MotorRight;
            }

            // 顶叶
            if (x >= RegionBounds.Parietal.minX && x <= RegionBounds.Parietal.maxX &&
                y >= RegionBounds.Parietal.minY && y <= RegionBounds.Parietal.maxY &&
                z >= RegionBounds.Parietal.minZ && z <= RegionBounds.Parietal.maxZ)
            {
                return x < 0 ? Region.ParietalLeft : Region.ParietalRight;
            }

            // 颞叶（左侧）
            if (x >= RegionBounds.Temporal.minX && x <= -20 &&
                y >= RegionBounds.Temporal.minY && y <= RegionBounds.Temporal.maxY &&
                z >= RegionBounds.Temporal.minZ && z <= RegionBounds.Temporal.maxZ)
            {
                return Region.TemporalLeft;
            }

            // 颞叶（右侧）
            if (x >= 20 && x <= 80 &&
                y >= RegionBounds.Temporal.minY && y <= RegionBounds.Temporal.maxY &&
                z >= RegionBounds.Temporal.minZ && z <= RegionBounds.Temporal.maxZ)
            {
                return Region.TemporalRight;
            }

            // 枕叶
            if (x >= RegionBounds.Occipital.minX && x <= RegionBounds.Occipital.maxX &&
                y >= RegionBounds.Occipital.minY && y <= RegionBounds.Occipital.maxY &&
                z >= RegionBounds.Occipital.minZ && z <= RegionBounds.Occipital.maxZ)
            {
                return x < 0 ? Region.OccipitalLeft : Region.OccipitalRight;
            }

            return Region.Unknown;
        }

        /// <summary>
        /// 获取脑区名称
        /// </summary>
        public static string GetRegionName(Region region)
        {//将枚举值转换为中文描述，便于显示。
            switch (region)
            {
                case Region.Unknown: return "未知区域";
                case Region.PrefrontalLeft: return "前额叶左侧";
                case Region.PrefrontalRight: return "前额叶右侧";
                case Region.MotorLeft: return "运动皮层左侧";
                case Region.MotorRight: return "运动皮层右侧";
                case Region.ParietalLeft: return "顶叶左侧";
                case Region.ParietalRight: return "顶叶右侧";
                case Region.TemporalLeft: return "颞叶左侧";
                case Region.TemporalRight: return "颞叶右侧";
                case Region.OccipitalLeft: return "枕叶左侧";
                case Region.OccipitalRight: return "枕叶右侧";
                default: return "未知区域";
            }
        }

        /// <summary>
        /// 判断两个位置是否在同一脑区
        /// </summary>
        public static bool AreInSameRegion(Vector3 pos1, Vector3 pos2)
        {
            return GetRegionFromPosition(pos1) == GetRegionFromPosition(pos2);
        }

        /// <summary>
        /// 判断位置是否在指定脑区
        /// </summary>
        public static bool IsInRegion(Vector3 position, Region region)
        {
            return GetRegionFromPosition(position) == region;
        }
    }
}