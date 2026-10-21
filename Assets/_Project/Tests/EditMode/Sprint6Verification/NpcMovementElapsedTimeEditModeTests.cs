using NUnit.Framework;

public sealed class NpcMovementElapsedTimeEditModeTests
{
    [Test]
    public void TwoNpcUpdatesAcrossOneHundredTicksAccountForAllTime()
    {
        var npc = new SystemNpcRuntimeState();
        SystemNpcMovementTime2A.Reset(npc, 0d);
        double movedTicks = 0d;
        foreach (double now in new[] { 20d, 100d })
        {
            double budget = SystemNpcMovementTime2A.Accrue(npc, now);
            movedTicks += budget;
            SystemNpcMovementTime2A.Consume(npc, budget);
        }
        Assert.That(movedTicks, Is.EqualTo(100d));
        Assert.That(npc.PendingMovementTicks, Is.Zero);
    }

    [Test]
    public void SystemVisitsDoNotCreditUnprocessedNpcTwice()
    {
        var frequent = new SystemNpcRuntimeState();
        var rare = new SystemNpcRuntimeState();
        SystemNpcMovementTime2A.Reset(frequent, 0d);
        SystemNpcMovementTime2A.Reset(rare, 0d);
        double frequentDistance = 0d;
        double rareDistance = 0d;
        for (int tick = 10; tick <= 100; tick += 10)
        {
            double credit = SystemNpcMovementTime2A.Accrue(frequent, tick);
            frequentDistance += credit * 100d;
            SystemNpcMovementTime2A.Consume(frequent, credit);
            if (tick == 20 || tick == 100)
            {
                credit = SystemNpcMovementTime2A.Accrue(rare, tick);
                rareDistance += credit * 100d;
                SystemNpcMovementTime2A.Consume(rare, credit);
            }
        }
        Assert.That(rareDistance, Is.EqualTo(10000d));
        Assert.That(rareDistance, Is.EqualTo(frequentDistance));
    }

    [Test]
    public void DeferredRouteBuildKeepsUnspentCredit()
    {
        var npc = new SystemNpcRuntimeState();
        SystemNpcMovementTime2A.Reset(npc, 0d);
        SystemNpcMovementTime2A.Accrue(npc, 40d);
        // No Consume: route work was deferred.
        Assert.That(SystemNpcMovementTime2A.Accrue(npc, 100d), Is.EqualTo(100d));
        SystemNpcMovementTime2A.Consume(npc, 25d);
        Assert.That(npc.PendingMovementTicks, Is.EqualTo(75d));
    }

    [Test]
    public void SameTimeAndFractionalFramesDoNotDuplicateCredit()
    {
        var npc = new SystemNpcRuntimeState();
        SystemNpcMovementTime2A.Reset(npc, 10d);
        Assert.That(SystemNpcMovementTime2A.Accrue(npc, 10.25d), Is.EqualTo(0.25d));
        Assert.That(SystemNpcMovementTime2A.Accrue(npc, 10.25d), Is.EqualTo(0.25d));
        SystemNpcMovementTime2A.Consume(npc, 0.25d);
        Assert.That(SystemNpcMovementTime2A.Accrue(npc, 10.5d), Is.EqualTo(0.25d));
    }

    [Test]
    public void SpawnAndPlanetStayDoNotCreateCreditFromBeforeMovement()
    {
        var npc = new SystemNpcRuntimeState();
        SystemNpcMovementTime2A.Reset(npc, 90d);
        Assert.That(SystemNpcMovementTime2A.Accrue(npc, 100d), Is.EqualTo(10d));
        SystemNpcMovementTime2A.Reset(npc, 150d);
        Assert.That(SystemNpcMovementTime2A.Accrue(npc, 160d), Is.EqualTo(10d));
    }

    [Test]
    public void ClockRollbackDoesNotCreateNegativeOrOldCredit()
    {
        var npc = new SystemNpcRuntimeState();
        SystemNpcMovementTime2A.Reset(npc, 100d);
        Assert.That(SystemNpcMovementTime2A.Accrue(npc, 50d), Is.Zero);
        Assert.That(SystemNpcMovementTime2A.Accrue(npc, 60d), Is.EqualTo(10d));
    }
}
