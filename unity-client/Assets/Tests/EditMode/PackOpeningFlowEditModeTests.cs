using AppreciatorsTcg.Packs;
using NUnit.Framework;

namespace AppreciatorsTcg.Tests.EditMode
{
    public class PackOpeningFlowEditModeTests
    {
        [Test]
        public void RitualRequiresSecondTapAndPreservesStageOrder()
        {
            PackOpeningFlow flow = new PackOpeningFlow();
            Assert.AreEqual(PackOpeningState.Sealed, flow.State);
            Assert.IsTrue(flow.TryBeginTear());
            Assert.IsFalse(flow.TryBeginTear(), "Duplicate taps must not restart a locked tear.");
            Assert.AreEqual(PackOpeningState.Tearing, flow.State);

            flow.MarkOpenGlow();
            Assert.AreEqual(PackOpeningState.OpenGlow, flow.State);
            flow.WaitForReveal();
            Assert.AreEqual(PackOpeningState.WaitingForReveal, flow.State);
            Assert.IsTrue(flow.TryConfirmReveal(), "The second allowed tap begins the reveal sequence.");
            Assert.IsFalse(flow.TryConfirmReveal(), "Duplicate confirmation taps must be ignored.");
            Assert.AreEqual(PackOpeningState.BannerEnter, flow.State);

            flow.MarkConfetti();
            Assert.AreEqual(PackOpeningState.Confetti, flow.State);
            flow.MarkRevealingCards();
            Assert.AreEqual(PackOpeningState.RevealingCards, flow.State);
            flow.MarkComplete();
            Assert.AreEqual(PackOpeningState.Complete, flow.State);
        }

        [Test]
        public void RitualResetReturnsToSealedPack()
        {
            PackOpeningFlow flow = new PackOpeningFlow();
            flow.TryBeginTear();
            flow.Reset();
            Assert.AreEqual(PackOpeningState.Sealed, flow.State);
            Assert.IsTrue(flow.TryBeginTear());
        }
    }
}
