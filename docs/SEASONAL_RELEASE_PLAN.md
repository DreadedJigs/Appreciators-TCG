# Seasonal Card Universe

The release manifest at `backend/data/release-plan.json` is the machine-readable source of truth for the planned 6,666-card universe.

## Structure

- 22 seasons, 303 cards per season, released on a four-season annual cadence.
- Each season contains 141 Common, 90 Uncommon, 48 Rare, 18 Epic, 5 Legendary, and 1 provisional Crown card, matching the supplied card meta workbook.
- Every serialized Crown requires an accessible Echo or Avatar with identical competitive rules.
- Set One reserves 54 cards for each pure Learn, Build, and Grow path; 36 for each two-path hybrid; 32 neutral/community cards; and one Crown.
- Supported roadmap formats are Standard, Seasonal, Singleton, Draft, and Sealed.

## Play rules represented in the prototype

- 30-card decks, with the leader outside the deck.
- Up to two copies of a normal card and one copy of a Legendary, Mythic, or Crown card.
- One hidden card is retained between turns, one card must be committed each turn, and the permanent board holds five cards per player.
- Growth is the sole score: Spotlight begins at 150 Growth and victory occurs at 200 Growth.
- Turn order is Draw, Learn, Build or Discard, Combat, Gather Growth, Tally Appreciation, Refresh, then End Turn. Opposing field cards exchange Attack against persistent Defense before surviving cards contribute Growth.

The prototype catalog remains a small vertical slice. New season content should be generated from reusable card chassis and validated against the release endpoint, `GET /api/releases/plan`, before import into Unity.
