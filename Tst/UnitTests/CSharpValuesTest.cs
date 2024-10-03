using NUnit.Framework;
using PChecker.PRuntime;
using PChecker.PRuntime.Values;

namespace UnitTests
{
    [TestFixture]
    public class CSharpValuesTest
    {
        [Test]
        public void TestPrtBoolOverloading()
        {
            PrtBool boolT = true;
            PrtBool boolF = false;
            Assert.AreEqual(boolT, boolF || boolT);
            Assert.AreEqual(boolT, boolT || boolF);
            Assert.AreEqual(boolT, !boolF);
            Assert.AreEqual(boolF, boolT && boolF);
            Assert.AreEqual(boolT, boolT && boolT);
        }

        [Test]
        public void TestPrtFloatComparisions()
        {
            PrtBool boolT = true;
            PrtBool boolF = false;
            Assert.AreEqual(boolT, PrtValues.Box(1.0) < PrtValues.Box(2.0));
            Assert.AreEqual(boolF, PrtValues.Box(1.0) < PrtValues.Box(1.0));
            Assert.AreEqual(boolT, PrtValues.Box(1.0) <= PrtValues.Box(1.0));
            Assert.AreEqual(boolT, PrtValues.Box(1.0) <= PrtValues.Box(2.0));
            Assert.AreEqual(boolT, PrtValues.Box(1.0) == PrtValues.Box(1.0));
            Assert.AreEqual(boolT, PrtValues.Box(1.0) != PrtValues.Box(2.0));
            Assert.AreEqual(boolT, PrtValues.Box(1.0) >= PrtValues.Box(1.0));
            Assert.AreEqual(boolT, PrtValues.Box(1.0) >= PrtValues.Box(0.0));
            Assert.AreEqual(boolT, PrtValues.Box(1.0) > PrtValues.Box(0.0));
        }

        [Test]
        public void TestPrtFloatOverloading()
        {
            PrtFloat float1 = 1.0;
            PrtFloat float2 = 2.0;
            PrtFloat float3 = 3.0;
            Assert.AreEqual(float3, float1 + float2);
            Assert.AreEqual(float1, float3 - float2);
            Assert.AreEqual(float2, float1 * float2);
            Assert.AreEqual(PrtValues.Box(1.5), float3 / float2);
            Assert.AreEqual(PrtValues.Box(6.0), float1 + float2 + float3);
            Assert.AreEqual(PrtValues.Box(6.0), float1 * float2 * float3);
            Assert.AreEqual(float1, float3 / (float1 + float2));
            Assert.AreEqual(-float1, float2 - float3);
            Assert.AreEqual(+float1, float1);
            Assert.AreEqual(float1, +float1);
        }

        [Test]
        public void TestPrtIntComparisions()
        {
            PrtBool boolT = true;
            PrtBool boolF = false;
            Assert.AreEqual(boolT, PrtValues.Box(1) < PrtValues.Box(2));
            Assert.AreEqual(boolF, PrtValues.Box(1) < PrtValues.Box(1));
            Assert.AreEqual(boolT, PrtValues.Box(1) <= PrtValues.Box(1));
            Assert.AreEqual(boolT, PrtValues.Box(1) <= PrtValues.Box(2));
            Assert.AreEqual(boolT, PrtValues.Box(1) == PrtValues.Box(1));
            Assert.AreEqual(boolT, PrtValues.Box(1) != PrtValues.Box(2));
            Assert.AreEqual(boolT, PrtValues.Box(1) >= PrtValues.Box(1));
            Assert.AreEqual(boolT, PrtValues.Box(1) >= PrtValues.Box(0));
            Assert.AreEqual(boolT, PrtValues.Box(1) > PrtValues.Box(0));
        }

        [Test]
        public void TestPrtIntOverloading()
        {
            PrtInt int1 = 1;
            PrtInt int2 = 2;
            PrtInt int3 = 3;
            Assert.AreEqual(int3, int1 + int2);
            Assert.AreEqual(int1, int3 - int2);
            Assert.AreEqual(int2, int1 * int2);
            Assert.AreEqual(int1, int3 / int2);
            Assert.AreEqual(PrtValues.Box(6), int1 + int2 + int3);
            Assert.AreEqual(PrtValues.Box(6), int1 * int2 * int3);
            Assert.AreEqual(int1, int3 / (int1 + int2));
            Assert.AreEqual(-int1, int2 - int3);
            Assert.AreEqual(+int1, int1);
            Assert.AreEqual(int1, +int1);
        }
    }
}