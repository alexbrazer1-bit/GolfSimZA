# GolfSim ZA — Phase 0.6

## Play Round

Phase 0.6 connects the 0.5 course/tee/round selection flow to a playable prototype round.

### Included
- Selected course, tee and 9/18-hole setting carry into gameplay.
- Hole number, par, tee colour and hole distance are displayed.
- Stroke count and running round score are tracked.
- 18-hole procedural demo hole distances are provided, with 9-hole rounds using holes 1–9.
- Tee colours alter prototype hole distance.
- Demo courses have small distance variations.
- Existing launch-monitor/test-shot flow remains available.
- SPACE launches a test shot after selecting a club with 1–8.
- A new shot cannot interrupt an active flight or roll.
- Carry and total distance remain live and the landing marker is retained.
- After a shot stops, the player can finish the hole and advance to the next hole.
- The round can be finished after the selected number of holes.

### Unity setup
Use **GolfSimZA → Create 0.6 Play Round** once after pulling the update. This creates:

`Assets/Scenes/GolfSimZA_0_6_PlayRound.unity`

The 0.5 course-selection scene now loads the 0.6 Play Round scene when **START ROUND** is pressed.

### Next planned stage
Phase 0.7 can replace the procedural straight-hole presentation with richer hole layouts, hazards and scoring/pin interaction while keeping the 0.6 round flow intact.
