using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

[RequireComponent(typeof(LineRenderer))]
public class BoneSolver : MonoBehaviour
{
    [Header("��������")]
    public GameObject startObject;  // ������壨B��
    public GameObject endObject;    // ĩ�����壨A��
    public int segmentCount = 20;  // ���Ӷ���
    public float ropeWidth = 0.1f; // ���ӵĿ���

    [Header("��������")]
    public Vector3 gravity = new Vector3(0, -9.81f, 0); // ����
    public float damping = 0.98f;                       // ����
    [Range(0, 1)] public float bendingStiffness = 0.5f; // ���ӵ�Ӳ��
    public int solverIterations = 15;                  // ����������

    [Header("��������")]
    public float pullThreshold = 5f;    // ��ʼʩ�������ľ���
    public float pullForce = 50f;       // ������С
    public float stopPullDistance = 1f; // ֹͣ�����ľ���

    private List<RopeSegment> segments = new List<RopeSegment>();
    private LineRenderer lineRenderer;

    private class RopeSegment
    {
        public Vector3 currentPos;   // ��ǰλ��
        public Vector3 previousPos; // ǰһ֡��λ��
        public Vector3 externalForce; // ������������

        public RopeSegment(Vector3 pos)
        {
            currentPos = pos;
            previousPos = pos;
            externalForce = Vector3.zero;
        }
    }

    void Start()
    {
        InitializeRope();
    }

    void FixedUpdate()
    {
        ApplyGravity();
        ApplyConstraints();
        HandleEndPoints();
        HandlePullForce();
        UpdateRenderer();
    }

    private void InitializeRope()
    {
        segments.Clear();

        Vector3 startPos = startObject.transform.position;
        Vector3 endPos = endObject.transform.position;

        for (int i = 0; i < segmentCount; i++)
        {
            float t = i / (float)(segmentCount - 1);
            Vector3 initialPos = Vector3.Lerp(startPos, endPos, t);
            segments.Add(new RopeSegment(initialPos));
        }

        lineRenderer = GetComponent<LineRenderer>();
        if (lineRenderer != null)
        {
            lineRenderer.positionCount = segmentCount;
            lineRenderer.startWidth = ropeWidth;
            lineRenderer.endWidth = ropeWidth;
        }
    }

    public void ApplyForceToSegment(int segmentIndex, Vector3 force)
    {
        if (segmentIndex >= 0 && segmentIndex < segments.Count)
        {
            segments[segmentIndex].externalForce += force;
        }
    }
    private void ApplyGravity()
    {
        for (int i = 1; i < segments.Count - 1; i++)
        {
            RopeSegment segment = segments[i];
            Vector3 velocity = (segment.currentPos - segment.previousPos) * damping;
            segment.previousPos = segment.currentPos;

            // ������������һ��Ӧ�õ��˶���
            segment.currentPos += velocity + gravity * Time.fixedDeltaTime * Time.fixedDeltaTime + segment.externalForce * Time.fixedDeltaTime;

            // ÿ֡������������ȷ����̬��
            segment.externalForce = Vector3.zero;
        }
    }

    private void ApplyConstraints()
    {
        for (int iteration = 0; iteration < solverIterations; iteration++)
        {
            for (int i = 0; i < segments.Count - 1; i++)
            {
                RopeSegment segmentA = segments[i];
                RopeSegment segmentB = segments[i + 1];

                Vector3 delta = segmentB.currentPos - segmentA.currentPos;
                float currentDistance = delta.magnitude;
                float targetDistance = Vector3.Distance(startObject.transform.position, endObject.transform.position) / segmentCount;

                // Ӧ��Ӳ��Լ��
                Vector3 correction = delta.normalized * (currentDistance - targetDistance) * bendingStiffness;

                if (i != 0)
                    segmentA.currentPos += correction * 0.5f;

                if (i != segments.Count - 2)
                    segmentB.currentPos -= correction * 0.5f;
            }
        }
    }

    private void HandleEndPoints()
    {
        // ���ֶ˵�λ��
        segments[0].currentPos = startObject.transform.position;
        segments[segments.Count - 1].currentPos = endObject.transform.position;
    }

    private void HandlePullForce()
    {
        float distance = Vector3.Distance(startObject.transform.position, endObject.transform.position);

        if (distance > pullThreshold)
        {
            Vector3 direction = (startObject.transform.position - endObject.transform.position).normalized;
            endObject.GetComponent<Rigidbody>().AddForce(direction * pullForce);
        }
        else if (distance < stopPullDistance)
        {
            // �������������˶���������ֱ��ֹͣ
            Vector3 direction = (startObject.transform.position - endObject.transform.position).normalized;
            endObject.GetComponent<Rigidbody>().linearVelocity = -direction * 0.1f; // ����΢�����ٶ�
        }
    }

    private void UpdateRenderer()
    {
        if (lineRenderer != null)
        {
            for (int i = 0; i < segments.Count; i++)
            {
                lineRenderer.SetPosition(i, segments[i].currentPos);
            }
        }
    }
}