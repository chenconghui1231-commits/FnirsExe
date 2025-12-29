using System;
using System.Collections.Generic;
using System.Linq;
//using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;
namespace FNIRS_OxygenAlgorithm
{
    /// <summary>
    /// 脑氧信号转换工具类（基于三波长修正朗伯-比尔定律）
    /// </summary>
    public class OxygenConverter
    {
        // 关键参数（可根据设备和实验调整）
        private const double DPF = 6.26;          // 差分路径因子（成人头皮组织典型值，通常为4-6）
        private const double Distance = 3.0;    // 光源-探测器距离（mm）根据论文数据
        private readonly double _pathLength;     // 光程长度 B = DPF × Distance（cm）

        // 摩尔消光系数（cm⁻¹·M⁻¹，3个波长：730nm、850nm、940nm）
        // 数据来源：文献值和标准消光系数表
        private readonly double[,] _extinctionCoeffs = new double[3, 3] {
            // HbO₂,    HbR,     H₂O (水)
            { 0.350,   0.890,   0.015 },   // 730nm
            { 0.361,   1.200,   0.020 },   // 850nm  
            { 0.280,   0.650,   0.045 }    // 940nm
        };
        private readonly double[] _incidentIntensity = new double[3] { 1000000.0, 1000000.0, 1000000.0 };

        public OxygenConverter()
        {
            _pathLength = DPF * (Distance / 10);  // 计算光程长度 B = DPF × d (d从mm转换为cm)
            Console.WriteLine($"光程长度: {_pathLength} cm");
        }

        private void SetIncidentIntensity(double[] incidentIntensity)
        {
            if (incidentIntensity.Length != 3)
                throw new ArgumentException("入射光强必须包含3个波长的值");

            _incidentIntensity[0] = incidentIntensity[0];
            _incidentIntensity[1] = incidentIntensity[1];
            _incidentIntensity[2] = incidentIntensity[2];
        }

        //转换数据求解浓度
        public double[] ConvertToHemoglobin(double[] rawIntensity)
        {
            if (rawIntensity.Length == 3)
            {
                // 原有的3波长处理
                Console.WriteLine($"接收到3波长光强数据: 730nm={rawIntensity[0]:F3}, 850nm={rawIntensity[1]:F3}, 940nm={rawIntensity[2]:F3}");
                double[] absorbance = CalculateAbsorbanceFromIntensity(rawIntensity);
                return ConvertFromAbsorbance(absorbance);
            }
            else if (rawIntensity.Length == 2)
            {
                // 新的2波长处理
                return ConvertTwoWavelengths(rawIntensity);
            }
            else
            {
                throw new ArgumentException("输入必须包含2个或3个波长的光强");
            }
        }

        /// <summary>
        /// 针对2波长系统(690nm, 830nm)的转换方法
        /// </summary>
        public double[] ConvertTwoWavelengths(double[] rawIntensity)
        {
            if (rawIntensity.Length != 2)
                throw new ArgumentException("输入必须包含2个波长的光强");

            Console.WriteLine($"接收到2波长光强数据: 690nm={rawIntensity[0]:F3}, 830nm={rawIntensity[1]:F3}");

            // 690nm和830nm的消光系数（需要验证这些值）
            double[,] extinctionCoeffs2WL = new double[2, 2] {
                // HbO₂,    HbR
                { 0.150,   0.380 },   // 690nm
                { 0.250,   0.850 }    // 830nm
            };

            // 计算吸光度
            double[] absorbance = new double[2];
            for (int i = 0; i < 2; i++)
            {
                if (rawIntensity[i] <= 0)
                    absorbance[i] = 0;
                else
                    absorbance[i] = Math.Log10(_incidentIntensity[i] / rawIntensity[i]);
            }

            // 使用2波长求解
            return SolveTwoWavelengthEquation(absorbance, extinctionCoeffs2WL);
        }

        public double[] ConvertFromAbsorbance(double[] absorbance)
        {
            if (absorbance.Length != 3)
                throw new ArgumentException("输入必须包含3个波长的吸光度");

            Console.WriteLine($"接收到吸光度数据: 730nm={absorbance[0]:F3}, 850nm={absorbance[1]:F3}, 940nm={absorbance[2]:F3}");

            // 使用吸光度A（即ΔOD）求解浓度变化
            double[] concentrationChanges = SolveThreeWavelengthEquation(absorbance);

            // 转换为微摩尔浓度（μM）
            double deltaCHbO_μM = concentrationChanges[0];
            double deltaCHbR_μM = concentrationChanges[1];
            double deltaCHbT_μM = deltaCHbO_μM + deltaCHbR_μM;

            return new double[] { deltaCHbO_μM, deltaCHbR_μM, deltaCHbT_μM };
        }

        private double[] CalculateAbsorbanceFromIntensity(double[] rawIntensity)
        {
            double[] absorbance = new double[3];
            for (int i = 0; i < 3; i++)
            {
                if (rawIntensity[i] <= 0)
                    absorbance[i] = 0;
                else
                    absorbance[i] = Math.Log10(_incidentIntensity[i] / rawIntensity[i]);
            }
            return absorbance;
        }

        private double[] SolveThreeWavelengthEquation(double[] deltaOD)
        {
            // 构建系数矩阵 A (3x3)
            Matrix<double> A = DenseMatrix.OfArray(new double[,]
            {
                { _extinctionCoeffs[0, 0], _extinctionCoeffs[0, 1], _extinctionCoeffs[0, 2] }, // 730nm: ε_HbO2, ε_Hb, ε_H2O
                { _extinctionCoeffs[1, 0], _extinctionCoeffs[1, 1], _extinctionCoeffs[1, 2] }, // 850nm: ε_HbO2, ε_Hb, ε_H2O
                { _extinctionCoeffs[2, 0], _extinctionCoeffs[2, 1], _extinctionCoeffs[2, 2] }  // 940nm: ε_HbO2, ε_Hb, ε_H2O
            });

            // 构建常数向量 b (3x1)
            Vector<double> b = DenseVector.OfArray(new double[]
            {
                deltaOD[0] / _pathLength, // ΔOD⁷³⁰ / B
                deltaOD[1] / _pathLength, // ΔOD⁸⁵⁰ / B
                deltaOD[2] / _pathLength  // ΔOD⁹⁴⁰ / B
            });

            try
            {
                // 使用直接矩阵求逆法: x = A⁻¹b
                // 检查矩阵是否可逆
                if (A.Determinant() < 1e-10)
                {
                    Console.WriteLine("警告：系数矩阵接近奇异，退回双波长方法");
                    // 使用730nm和850nm的消光系数作为备用
                    return SolveTwoWavelengthEquation(new double[] { deltaOD[0], deltaOD[1] },
                        new double[,] {
                            { _extinctionCoeffs[0, 0], _extinctionCoeffs[0, 1] },
                            { _extinctionCoeffs[1, 0], _extinctionCoeffs[1, 1] }
                        });
                }

                Matrix<double> A_inv = A.Inverse();
                Console.WriteLine("逆矩阵:");
                Console.WriteLine(A_inv);
                Vector<double> x = A_inv * b;
                Console.WriteLine("解向量:");
                Console.WriteLine(x);
                return x.ToArray(); // [ΔC_HbO₂, ΔC_Hb, ΔC_H₂O]
            }
            catch (Exception ex)
            {
                Console.WriteLine($"矩阵求逆失败: {ex.Message}，退回双波长方法");
                // 使用730nm和850nm的消光系数作为备用
                return SolveTwoWavelengthEquation(new double[] { deltaOD[0], deltaOD[1] },
                    new double[,] {
                        { _extinctionCoeffs[0, 0], _extinctionCoeffs[0, 1] },
                        { _extinctionCoeffs[1, 0], _extinctionCoeffs[1, 1] }
                    });
            }
        }

        /// <summary>
        /// 改进的双波长求解方法（支持自定义消光系数）
        /// </summary>
        private double[] SolveTwoWavelengthEquation(double[] deltaOD, double[,] extinctionCoeffs)
        {
            if (deltaOD.Length != 2)
                throw new ArgumentException("吸光度数据必须包含2个波长");

            if (extinctionCoeffs.GetLength(0) != 2 || extinctionCoeffs.GetLength(1) != 2)
                throw new ArgumentException("消光系数矩阵必须是2x2");

            // 使用传入的消光系数
            double eHbO1 = extinctionCoeffs[0, 0];
            double eHbR1 = extinctionCoeffs[0, 1];
            double eHbO2 = extinctionCoeffs[1, 0];
            double eHbR2 = extinctionCoeffs[1, 1];

            Console.WriteLine($"使用消光系数 - 波长1: HbO={eHbO1:F3}, HbR={eHbR1:F3}");
            Console.WriteLine($"使用消光系数 - 波长2: HbO={eHbO2:F3}, HbR={eHbR2:F3}");
            Console.WriteLine($"吸光度数据: OD1={deltaOD[0]:F6}, OD2={deltaOD[1]:F6}");

            // 构建2×2系数矩阵
            double[,] extinctionMatrix = new double[2, 2] {
                { eHbO1, eHbR1 },
                { eHbO2, eHbR2 }
            };

            // 计算矩阵行列式
            double det = extinctionMatrix[0, 0] * extinctionMatrix[1, 1]
                       - extinctionMatrix[0, 1] * extinctionMatrix[1, 0];

            Console.WriteLine($"系数矩阵行列式: {det:E6}");

            if (Math.Abs(det) < 1e-10)
            {
                Console.WriteLine("警告：系数矩阵接近奇异，返回零值");
                return new double[] { 0, 0, 0 };
            }

            // 计算逆矩阵
            double[,] inverseMatrix = new double[2, 2] {
                { extinctionMatrix[1, 1] / det, -extinctionMatrix[0, 1] / det },
                { -extinctionMatrix[1, 0] / det, extinctionMatrix[0, 0] / det }
            };

            Console.WriteLine($"逆矩阵: [{inverseMatrix[0, 0]:F3}, {inverseMatrix[0, 1]:F3}; {inverseMatrix[1, 0]:F3}, {inverseMatrix[1, 1]:F3}]");

            // 求解浓度变化
            double deltaCHbO = (inverseMatrix[0, 0] * deltaOD[0] + inverseMatrix[0, 1] * deltaOD[1]) / _pathLength;
            double deltaCHbR = (inverseMatrix[1, 0] * deltaOD[0] + inverseMatrix[1, 1] * deltaOD[1]) / _pathLength;
            double deltaCHbT = deltaCHbO + deltaCHbR;

            Console.WriteLine($"计算得到的浓度变化: ΔHbO={deltaCHbO:E6}, ΔHbR={deltaCHbR:E6}, ΔHbT={deltaCHbT:E6}");

            return new double[] { deltaCHbO, deltaCHbR, deltaCHbT };
        }

        /// <summary>
        /// 计算脑组织氧饱和度（rSO₂）
        /// </summary>
        /// <param name="concentration">浓度（[HbO₂, HbR, HbT]，单位μM）</param>
        /// <returns>氧饱和度（百分比）</returns>
        public double CalculateOxygenSaturation(double[] concentration)
        {
            if (concentration.Length < 2)
                throw new ArgumentException("浓度数据必须包含HbO₂和HbR值");

            double totalHb = concentration[0] + concentration[1];
            if (Math.Abs(totalHb) < 1e-10)
                return 0;

            return (concentration[0] / totalHb) * 100;
        }

        /// <summary>
        /// 批量转换多帧数据
        /// </summary>
        /// <param name="rawFrames">原始光强帧列表（每帧为[I_730, I_850, I_940]）</param>
        /// <returns>批量浓度变化（每帧为[ΔHbO₂, ΔHbR, ΔHbT]）</returns>
        public List<double[]> ConvertBatch(List<double[]> rawFrames)
        {
            var result = new List<double[]>();
            foreach (var frame in rawFrames)
            {
                result.Add(ConvertToHemoglobin(frame));
            }
            return result;
        }

        public double[] CalculateDifferentialOD(double[] intensity1, double[] intensity2)
        {
            if (intensity1.Length != 3 || intensity2.Length != 3)
                throw new ArgumentException("光强数据必须包含3个波长");

            double[] deltaOD = new double[3];
            for (int i = 0; i < 3; i++)
            {
                if (intensity1[i] <= 0 || intensity2[i] <= 0)
                    deltaOD[i] = 0;
                else
                    deltaOD[i] = Math.Log10(intensity2[i] / intensity1[i]); // ΔOD = log₁₀(I₂/I₁)
            }
            return deltaOD;
        }
    }
}