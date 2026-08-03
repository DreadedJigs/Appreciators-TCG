# Appreciators Originals Metadata Audit

Generated: 2026-06-20T22:32:52.858Z

## Contract

- Collection: Appreciators Originals (OG)
- ApeChain mainnet chain ID: 33139
- Contract: [0xd92b263b48f74d0cd21f9d2c01b6cd06f2ab96cd](https://apescan.io/address/0xd92b263b48f74d0cd21f9d2c01b6cd06f2ab96cd)
- Standard: ERC-721
- Total supply: 6,666
- Imported tokens: 6,666
- Excluded by policy: 0
- Images are referenced by URL and are not copied into the repository.

## Import Policy

Only the gameplay traits explicitly approved for Appreciators TCG are mapped to gameplay card IDs. Other on-chain attributes remain catalog metadata and do not automatically become mechanics. Any token or metadata containing `Dreaded Ape` is excluded case-insensitively.

## Approved Gameplay Mapping

| Gameplay card | Group | Status | Matching tokens | On-chain values |
|---|---|---:|---:|---|
| Ghost Companion | Companion | matched | 139 | Companion: Ghost (139) |
| Pigeon Companion | Companion | matched | 118 | Companion: Pigeon (118) |
| Cat Companion | Companion | matched | 82 | Companion: Cat (82) |
| Devil Dog Companion | Companion | matched | 76 | Companion: DevilDog (76) |
| Snake Companion | Companion | matched | 57 | Companion: Snake (57) |
| Great White Head | Head | matched | 322 | Head: Great White (138); Head: Epic Great White (109); Head: Legendary Great White (75) |
| Tiger Shark Head | Head | matched | 68 | Head: Tiger Shark (68) |
| Unicorn Head | Head | matched | 65 | Head: Unicorn (65) |
| Alpha Kaiju Head | Head | matched | 157 | Head: Alpha Kaiju (83); Head: Alpha Kaiju w/ Shooting Stars (74) |
| Regular Body | Body | not_present_in_originals_metadata | 0 | None |
| No Head Body | Body | matched | 1102 | Body State: Headless (1102) |
| Decapitated Body | Body | matched | 12 | Body State: Decapitated (12) |
| Blockchain Background | Background | not_present_in_originals_metadata | 0 | None |
| Ghost Flame Background | Background | not_present_in_originals_metadata | 0 | None |
| Pink Lemonade Background | Background | matched | 294 | Background: Pink Lemonade (294) |
| Tropical Background | Background | matched | 282 | Background: Tropical (282) |
| Overcast Background | Background | matched | 908 | Background: Overcast (908) |
| Second Hand Smoke Dawn | Background | matched | 231 | Background: Second Hand Smoke Dawn (231) |
| Second Hand Smoke Seafoam | Background | matched | 190 | Background: Second Hand Smoke Seafoam (190) |
| Green Skin | Skin Tone | not_present_in_originals_metadata | 0 | None |
| Blue Skin | Skin Tone | not_present_in_originals_metadata | 0 | None |
| Purple Skin | Skin Tone | matched | 1459 | GRP Color: Purple (1459) |
| Pink Skin | Skin Tone | not_present_in_originals_metadata | 0 | None |
| Yellow Skin | Skin Tone | not_present_in_originals_metadata | 0 | None |
| White Skin | Skin Tone | not_present_in_originals_metadata | 0 | None |
| CHAOS | Special 1/1 | matched | 1 | Name: Chaos (1) |
| CAPTAIN FISH FOOD | Special 1/1 | matched | 1 | Name: Captain Fish Food (1) |
| THE ORIGINAL | Special 1/1 | not_present_in_originals_metadata | 0 | None |

## Catalog Summary

- Trait types: 18
- Unique trait values: 281
- Gameplay definitions audited: 29
- Missing approved gameplay definitions: None
- Unapproved gameplay definitions: None

## Refresh

From the repository root:

```powershell
node scripts/import-apechain-originals.mjs
```

This metadata snapshot is suitable for local ownership previews and future wallet sync. Production ownership verification must query ApeChain or a trusted indexer server-side rather than trusting client data.
