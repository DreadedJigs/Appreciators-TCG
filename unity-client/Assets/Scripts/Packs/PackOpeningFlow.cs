namespace AppreciatorsTcg.Packs
{
    public enum PackOpeningState
    {
        Sealed,
        Tearing,
        OpenGlow,
        WaitingForReveal,
        BannerEnter,
        Confetti,
        RevealingCards,
        Complete
    }

    /// <summary>Small deterministic gate for ritual input and animation order.</summary>
    public sealed class PackOpeningFlow
    {
        public PackOpeningState State { get; private set; } = PackOpeningState.Sealed;

        public bool TryBeginTear()
        {
            if (State != PackOpeningState.Sealed) return false;
            State = PackOpeningState.Tearing;
            return true;
        }

        public void MarkOpenGlow()
        {
            if (State == PackOpeningState.Tearing) State = PackOpeningState.OpenGlow;
        }

        public void WaitForReveal()
        {
            if (State == PackOpeningState.OpenGlow) State = PackOpeningState.WaitingForReveal;
        }

        public bool TryConfirmReveal()
        {
            if (State != PackOpeningState.WaitingForReveal) return false;
            State = PackOpeningState.BannerEnter;
            return true;
        }

        public void MarkConfetti()
        {
            if (State == PackOpeningState.BannerEnter) State = PackOpeningState.Confetti;
        }

        public void MarkRevealingCards()
        {
            if (State == PackOpeningState.Confetti) State = PackOpeningState.RevealingCards;
        }

        public void MarkComplete()
        {
            if (State == PackOpeningState.RevealingCards) State = PackOpeningState.Complete;
        }

        public void Reset()
        {
            State = PackOpeningState.Sealed;
        }
    }
}
