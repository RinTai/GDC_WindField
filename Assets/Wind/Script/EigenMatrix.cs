using Unity.Mathematics;
using UnityEngine;

namespace MatrixEigen
{
    public static class EigenMatrix
    {
        /// <summary>
        /// 构建given矩阵 //0<=i<j<=3
        /// </summary>
        /// <param name="i">0-2之间的整数</param>
        /// <param name="j">1-3之间的整数 大于i</param>
        /// <param name="rad">弧度</param>
        /// <returns></returns>
        public static float4x4 GivenMatrix(int i, int j, float rad)
        {
            if (i < 0 || i > 2 || j < 1 || j > 3 || i >= j)
            {
                Debug.LogError("Index超过限制");
                return float4x4.identity;
            }

            float c = math.cos(rad);
            float s = math.sin(rad);

            float4x4 t = float4x4.identity;
            float4 c1, c2;

            switch (i)
            {
                case 0:
                    c1 = c * float4x4.identity.c0;
                    c1 += s * (j == 1 ? float4x4.identity.c1 : j == 2 ? float4x4.identity.c2 : float4x4.identity.c3);
                    t.c0 = c1;
                    c2 = -s * float4x4.identity.c0;
                    c2 += c * (j == 1 ? float4x4.identity.c1 : j == 2 ? float4x4.identity.c2 : float4x4.identity.c3);

                    break;
                case 1:
                    c1 = c * float4x4.identity.c1;
                    c1 += s * (j == 2 ? float4x4.identity.c2 : float4x4.identity.c3);
                    t.c1 = c1;
                    c2 = -s * float4x4.identity.c1;
                    c2 += c * (j == 2 ? float4x4.identity.c2 : float4x4.identity.c3);
                    break;
                //case 2:
                default:
                    c1 = c * float4x4.identity.c2;
                    c1 += s * (float4x4.identity.c3);
                    t.c2 = c1;
                    c2 = -s * float4x4.identity.c2;
                    c2 += c * (float4x4.identity.c3);
                    break;
            }

            switch (j)
            {
                case 1:
                    t.c1 = c2;
                    break;
                case 2:
                    t.c2 = c2;
                    break;
                //case 3:
                default:
                    t.c3 = c2;
                    break;
            }

            return t;
        }



        public static float2x2 Givens(float2 vec)
        {

            float denom = math.sqrt(vec.x * vec.x + vec.y * vec.y);
            if (denom <= 0)//为0向量
            {
                return float2x2.identity;
            }
            float c = vec.x / denom;
            float s = vec.y / denom;
            return new float2x2(c, s,
               -s, c);
        }



        /// <summary>
        /// 拿到一个4维向量的多个given矩阵 是正交阵 行列式为1 使得该四位向量经过该矩阵变换后 其第1维到第n-1维的值都为0
        /// </summary>
        /// <param name="vec"></param>
        /// <returns></returns>
        public static float4x4 Givens(float4 vec)
        {
            float denom03 = math.sqrt(vec.x * vec.x + vec.y * vec.y + vec.z * vec.z + vec.w * vec.w);

            if (denom03 <= 0)//为0向量
            {
                return float4x4.identity;
            }
            float denom02 = math.sqrt(vec.x * vec.x + vec.y * vec.y + vec.z * vec.z);
            float c, s;
            c = denom02 / denom03;
            s = vec.w / denom03;
            float4x4 T03 = new float4x4(c, 0, 0, s,
                                      0, 1, 0, 0,
                                      0, 0, 1, 0,
                                     -s, 0, 0, c);

            if (denom02 <= 0)
            {
                return T03;
            }
            float denom01 = math.sqrt(vec.x * vec.x + vec.y * vec.y);

            c = denom01 / denom02;
            s = vec.z / denom02;
            float4x4 T02 = new float4x4(c, 0, s, 0,
                                      0, 1, 0, 0,
                                     -s, 0, c, 0,
                                      0, 0, 0, 1);

            if (denom01 <= 0)
            {
                return math.mul(T03, T02);
            }

            c = vec.x / denom01;
            s = vec.y / denom01;
            float4x4 T01 = new float4x4(c, s, 0, 0,
                                      -s, c, 0, 0,
                                      0, 0, 1, 0,
                                      0, 0, 0, 1);//初等旋转矩阵
            return math.mul(T03, math.mul(T02, T01));
        }



        public static float3x3 Givens(float3 vec)
        {
            float denom02 = math.sqrt(vec.x * vec.x + vec.y * vec.y + vec.z * vec.z);
            if (denom02 <= 0)//为0向量
            {
                return float3x3.identity;
            }
            float denom01 = math.sqrt(vec.x * vec.x + vec.y * vec.y);
            float c, s;
            c = denom01 / denom02;
            s = vec.z / denom02;
            float3x3 T02 = new float3x3(c, 0, s,
                                       0, 1, 0,
                                      -s, 0, c);

            if (denom01 <= 0)
            {
                return T02;
            }

            c = vec.x / denom01;
            s = vec.y / denom01;
            float3x3 T01 = new float3x3(c, s, 0,
               -s, c, 0,
               0, 0, 1);//初等旋转矩阵

            return math.mul(T02, T01);
        }


        /// <summary>
        /// P-1 * A * P
        /// </summary>
        /// <param name="a">A矩阵 原始被变换的矩阵</param>
        /// <param name="p">P矩阵 对称阵 这里是Givens矩阵</param>
        /// <returns></returns>
        public static float4x4 PT_A_P(float4x4 a, float4x4 p)
        {
            float4x4 pT = math.transpose(p);
            return math.mul(pT, math.mul(a, p));
        }

        /// <summary>
        ///  计算旋转角度
        /// </summary>
        /// <param name="i"></param>
        /// <param name="j"></param>
        /// <param name="m"></param>
        /// <returns></returns>
        public static float GivenThelta(int i, int j, float4x4 m)
        {
            float mii = m[i][i];
            float mjj = m[j][j];
            if (math.abs(mjj - mii) < float.Epsilon)
            {
                return math.PI / 4.0F;
            }
            float mji = m[j][i];
            float mij = m[i][j];

            float thelta = 2 * mij / (mii - mjj);
            return 0.5f * math.atan(thelta);
        }
        /// <summary>
        /// 构造Given矩阵
        /// </summary>
        /// <param name="i"></param>
        /// <param name="j"></param>
        /// <param name="m"></param>
        /// <returns></returns>
        public static float4x4 GivenMatrix(int i, int j, float4x4 m)
        {
            float rad = GivenThelta(i, j, m);
            return GivenMatrix(i, j, rad);
        }

        /// <summary>
        /// 单次迭代
        /// 相对于贪心算法 这个少了个寻找最大值的过程 增加了个阈值判断
        /// </summary>
        /// <param name="m"></param>
        /// <returns></returns>
        static float4x4 JacobiMatrix(float4x4 m)
        {
            float4x4 P = float4x4.identity;
            for (int i = 0; i <= 2; i++)
            {
                for (int j = i + 1; j <= 3; j++)
                {
                    if (math.abs(m[i][j]) <= float.Epsilon) continue;

                    float4x4 p = GivenMatrix(i, j, m);
                    m = PT_A_P(m, p);
                    P = math.mul(P, p);//注意这里是右乘
                }
            }
            return P;
        }

        /// <summary>
        /// 计算矩阵的非对角元素的平方和
        /// </summary>
        /// <param name="m"></param>
        /// <returns></returns>
        public static float NonDiagonalElementsSum(float4x4 m)
        {
            float sum = 0;
            for (int i = 0; i <= 2; i++)
            {
                for (int j = i + 1; j <= 3; j++)
                {
                    sum += math.pow(m[i][j], 2);
                }
            }
            return sum;
        }

        /// <summary>
        /// 多次迭代的
        /// </summary>
        /// <param name="m"></param>
        /// <returns></returns>
        public static float4x4 JacobiMatrixs(float4x4 m)
        {
            float4x4 _P = float4x4.identity;
            float s;
            int num = 0;
            do
            {
                float4x4 p = JacobiMatrix(m);
                m = PT_A_P(m, p);
                _P = math.mul(_P, p);//注意这里是右乘
                s = NonDiagonalElementsSum(m);
                //Debug.Log(s);
                num++;
            }
            while (s > float.Epsilon);

            return _P;
        }
    }


}



