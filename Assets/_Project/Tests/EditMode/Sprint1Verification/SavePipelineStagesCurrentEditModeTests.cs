using System;
using NUnit.Framework;

namespace StarFrontier.Tests.Sprint1
{
    [TestFixture]
    public sealed class SavePipelineStagesCurrentEditModeTests
    {
        [Test]
        public void ValidateVersion_NullState_IsRejected()
        {
            SaveValidationResult result = new SaveValidationStage().ValidateVersion(null);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.BuildErrorMessage(), Does.Contain("null"));
        }

        [Test]
        public void ValidateVersion_FutureVersion_IsRejectedWithoutMutation()
        {
            var state = new GameRuntimeState();
            int futureVersion = SaveDataVersions.Current + 1;
            state.Meta.SaveDataVersion = futureVersion;

            SaveValidationResult result = new SaveValidationStage().ValidateVersion(state);

            Assert.That(result.IsValid, Is.False);
            Assert.That(state.Meta.SaveDataVersion, Is.EqualTo(futureVersion));
        }

        [Test]
        public void ValidateAndNormalize_InvalidPlayerRanges_AreClamped()
        {
            var state = new GameRuntimeState();
            state.Player.Level = -10;
            state.Player.Experience = -20;
            state.Player.Credits = -30;

            SaveValidationResult result =
                new SaveValidationStage().ValidateAndNormalize(state);

            Assert.That(result.IsValid, Is.True);
            Assert.That(state.Player.Level, Is.EqualTo(1));
            Assert.That(state.Player.Experience, Is.Zero);
            Assert.That(state.Player.Credits, Is.Zero);
            Assert.That(result.Normalizations, Is.Not.Empty);
        }

        [Test]
        public void ValidateAndNormalize_NullMarkets_AreRecreated()
        {
            var state = new GameRuntimeState { Markets = null };

            SaveValidationResult result =
                new SaveValidationStage().ValidateAndNormalize(state);

            Assert.That(result.IsValid, Is.True);
            Assert.That(state.Markets, Is.Not.Null);
        }

        [Test]
        public void Integrity_StampedState_VerifiesAsValid()
        {
            var state = new GameRuntimeState();
            var integrity = new SaveIntegrityStage();

            integrity.Stamp(state);

            Assert.That(state.Meta.IntegrityChecksum, Has.Length.EqualTo(64));
            Assert.That(integrity.Verify(state), Is.EqualTo(SaveIntegrityStatus.Valid));
        }

        [Test]
        public void Integrity_TamperedState_IsRejectedAndChecksumIsPreserved()
        {
            var state = new GameRuntimeState();
            var integrity = new SaveIntegrityStage();
            integrity.Stamp(state);
            string checksum = state.Meta.IntegrityChecksum;

            state.Player.Credits++;
            SaveIntegrityStatus status = integrity.Verify(state);

            Assert.That(status, Is.EqualTo(SaveIntegrityStatus.Invalid));
            Assert.That(state.Meta.IntegrityChecksum, Is.EqualTo(checksum));
        }

        [Test]
        public void Integrity_MissingChecksum_IsReportedSeparately()
        {
            var state = new GameRuntimeState();
            state.Meta.IntegrityChecksum = string.Empty;

            Assert.That(
                new SaveIntegrityStage().Verify(state),
                Is.EqualTo(SaveIntegrityStatus.Missing));
        }

        [Test]
        public void Integrity_StampWithoutState_Throws()
        {
            Assert.Throws<ArgumentException>(() => new SaveIntegrityStage().Stamp(null));
        }
    }
}
