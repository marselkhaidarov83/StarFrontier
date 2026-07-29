using NUnit.Framework;

public sealed class FuelHudState2AEditModeTests
{
    [Test]
    public void GetStateByPercent_ReturnsNormal_WhenPercentIs75()
    {
        FuelHudState2A state =
            FuelHudStateUtility2A.GetStateByPercent(75f);

        Assert.AreEqual(
            FuelHudState2A.Normal,
            state);
    }

    [Test]
    public void GetStateByPercent_ReturnsNormal_WhenPercentIs100()
    {
        FuelHudState2A state =
            FuelHudStateUtility2A.GetStateByPercent(100f);

        Assert.AreEqual(
            FuelHudState2A.Normal,
            state);
    }

    [Test]
    public void GetStateByPercent_ReturnsMedium_WhenPercentIs25()
    {
        FuelHudState2A state =
            FuelHudStateUtility2A.GetStateByPercent(25f);

        Assert.AreEqual(
            FuelHudState2A.Medium,
            state);
    }

    [Test]
    public void GetStateByPercent_ReturnsMedium_WhenPercentIs50()
    {
        FuelHudState2A state =
            FuelHudStateUtility2A.GetStateByPercent(50f);

        Assert.AreEqual(
            FuelHudState2A.Medium,
            state);
    }

    [Test]
    public void GetStateByPercent_ReturnsLow_WhenPercentIsBelow25()
    {
        FuelHudState2A state =
            FuelHudStateUtility2A.GetStateByPercent(24.99f);

        Assert.AreEqual(
            FuelHudState2A.Low,
            state);
    }

    [Test]
    public void GetStateByPercent_ReturnsLow_WhenPercentIsZero()
    {
        FuelHudState2A state =
            FuelHudStateUtility2A.GetStateByPercent(0f);

        Assert.AreEqual(
            FuelHudState2A.Low,
            state);
    }

    [Test]
    public void GetState_ClampsFuelAboveCapacity()
    {
        FuelHudState2A state =
            FuelHudStateUtility2A.GetState(
                currentFuel: 150,
                fuelCapacity: 100);

        Assert.AreEqual(
            FuelHudState2A.Normal,
            state);
    }

    [Test]
    public void GetState_ClampsNegativeFuel()
    {
        FuelHudState2A state =
            FuelHudStateUtility2A.GetState(
                currentFuel: -10,
                fuelCapacity: 100);

        Assert.AreEqual(
            FuelHudState2A.Low,
            state);
    }

    [Test]
    public void GetState_ReturnsLow_WhenCapacityIsZero()
    {
        FuelHudState2A state =
            FuelHudStateUtility2A.GetState(
                currentFuel: 10,
                fuelCapacity: 0);

        Assert.AreEqual(
            FuelHudState2A.Low,
            state);
    }

    [Test]
    public void BuildFuelLabel_ReturnsReadableText()
    {
        string label =
            FuelHudStateUtility2A.BuildFuelLabel(
                currentFuel: 30,
                fuelCapacity: 100);

        Assert.AreEqual(
            "Топливо: 30 / 100",
            label);
    }
}