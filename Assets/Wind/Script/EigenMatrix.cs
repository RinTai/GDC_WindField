using Unity.Mathematics;
using UnityEngine;

namespace MatrixEigen
{
    public static class EigenMatrix
    {
        /// <summary>
        /// ����given���� //0<=i<j<=3
        /// </summary>
        /// <param name="i">0-2֮�������</param>
        /// <param name="j">1-3֮������� ����i</param>
        /// <param name="rad">����</param>
        /// <returns></returns>
        public static float4x4 GivenMatrix(int i, int j, float rad)
        {
            if (i < 0 || i > 2 || j < 1 || j > 3 || i >= j)
            {
                Debug.LogError("Index��������");
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
            if (denom <= 0)//Ϊ0����
            {
                return float2x2.identity;
            }
            float c = vec.x / denom;
            float s = vec.y / denom;
            return new float2x2(c, s,
               -s, c);
        }



        /// <summary>
        /// �õ�һ��4ά�����Ķ��given���� �������� ����ʽΪ1 ʹ�ø���λ���������þ���任�� ���1ά����n-1ά��ֵ��Ϊ0
        /// </summary>
        /// <param name="vec"></param>
        /// <returns></returns>
        public static float4x4 Givens(float4 vec)
        {
            float denom03 = math.sqrt(vec.x * vec.x + vec.y * vec.y + vec.z * vec.z + vec.w * vec.w);

            if (denom03 <= 0)//Ϊ0����
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
                                      0, 0, 0, 1);//������ת����
            return math.mul(T03, math.mul(T02, T01));
        }



        public static float3x3 Givens(float3 vec)
        {
            float denom02 = math.sqrt(vec.x * vec.x + vec.y * vec.y + vec.z * vec.z);
            if (denom02 <= 0)//Ϊ0����
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
               0, 0, 1);//������ת����

            return math.mul(T02, T01);
        }


        /// <summary>
        /// P-1 * A * P
        /// </summary>
        /// <param name="a">A���� ԭʼ���任�ľ���</param>
        /// <param name="p">P���� �Գ��� ������Givens����</param>
        /// <returns></returns>
        public static float4x4 PT_A_P(float4x4 a, float4x4 p)
        {
            float4x4 pT = math.transpose(p);
            return math.mul(pT, math.mul(a, p));
        }

        /// <summary>
        ///  ������ת�Ƕ�
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
        /// ����Given����
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
        /// ���ε���
        /// �����̰���㷨 ������˸�Ѱ�����ֵ�Ĺ��� �����˸���ֵ�ж�
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
                    P = math.mul(P, p);//ע���������ҳ�
                }
            }
            return P;
        }

        /// <summary>
        /// �������ķǶԽ�Ԫ�ص�ƽ����
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
        /// ��ε�����
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
                _P = math.mul(_P, p);//ע���������ҳ�
                s = NonDiagonalElementsSum(m);
                //Debug.Log(s);
                num++;
            }
            while (s > float.Epsilon);

            return _P;
        }
    }


}



