using System;
using NUnit.Framework;
using UnityEngine;

namespace DefenseBattle.View.Tests
{
    /// <summary>할당 없는 동적 메시 도형 (render.ts 캔버스 도형 대신)</summary>
    public class ShapeBatchTests
    {
        GameObject m_go;
        ShapeBatch m_batch;

        [SetUp]
        public void SetUp()
        {
            m_go = new GameObject("ShapeBatchTest");
            m_batch = m_go.AddComponent<ShapeBatch>();
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(m_go);

        [Test]
        public void ShapeBatch_CountsVertices()
        {
            m_batch.Begin();
            Assert.AreEqual(0, m_batch.VertexCount);
            m_batch.FillCircle(100, 100, 20, new Color32(255, 0, 0, 255));
            int afterCircle = m_batch.VertexCount;
            Assert.AreEqual(ShapeBatch.Segments(20) + 1, afterCircle);
            m_batch.Line(0, 0, 50, 50, 3, new Color32(255, 255, 255, 255));
            Assert.AreEqual(afterCircle + 4, m_batch.VertexCount);
            m_batch.End();
            Assert.AreEqual(m_batch.VertexCount, m_go.GetComponent<MeshFilter>().sharedMesh.vertexCount);
            m_batch.Begin();
            Assert.AreEqual(0, m_batch.VertexCount);
        }

        [Test]
        public void ShapeBatch_NoAllocAfterWarmup()
        {
            DrawFrame();
            DrawFrame();
            long before = GC.GetAllocatedBytesForCurrentThread();
            DrawFrame();
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0, allocated);
        }

        void DrawFrame()
        {
            var c = new Color32(10, 20, 30, 200);
            m_batch.Begin();
            for (int i = 0; i < 200; i++)
            {
                m_batch.FillCircle(i, i, 12, c);
                m_batch.StrokeCircle(i, i, 18, 3, c);
                m_batch.FillEllipse(i, i, 10, 4, c);
                m_batch.StrokeEllipse(i, i, 10, 4, 2, c);
                m_batch.FillRect(i, i, 8, 8, c);
                m_batch.Line(i, 0, 0, i, 2, c);
                m_batch.StrokeArc(i, i, 22, -0.9f, 0.9f, 4, c);
                m_batch.FillTriangle(new Vector2(0, 0), new Vector2(i, 0), new Vector2(0, i), c);
                m_batch.RadialCircle(i, i, 30, c, new Color32(0, 0, 0, 0), 0.3f);
                m_batch.VerticalGradientRect(i, i, 10, 40, c, new Color32(0, 0, 0, 0));
            }
            m_batch.End();
        }
    }
}
