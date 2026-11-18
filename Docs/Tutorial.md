# Tutorial prompts

Explanations, results, retry guidance and the final handover pause the game until a fresh **Enter** (or numpad Enter) press. There is no reading timeout. A held Enter cannot dismiss the next card; mouse clicks cannot dismiss explanations or spend beer while reading.

The two lessons are labelled **1 / 2 — Stop the charge** and **2 / 2 — Turn the punch against them**. Short practice objectives remain visible until their condition completes. Beer/unbeer practice uses the normal PlayerAim range and obstruction checks: out of reach, gameplay slows to 35% so the player can reposition; in reach, time pauses to allow the parry. An enemy must actually be attacking before the beer exercise freezes. A footer explains which state is active.

Long explanations about dodging and lining up another enemy happen with game time paused, so neither attacks nor beer timers run down while reading. Side hints do not compete with the main lesson. Success and retry cards explain what happened and wait for the player; the next fight also waits for confirmation.

The lower-screen card has a lesson heading, short body, highlighted control labels and a status footer. It scales with the screen and leaves the aiming reticle, ingredient inventory and health/beer display clear.

Inspector: TutorialPrompter **Practice Pacing** controls positioning speed and the return to normal speed. TutorialDirector **Pacing** controls the quiet gaps between lessons and before completion. Obsolete automatic reading-time settings have been removed.

Validation: **Hold My Beer → Run Tutorial Prompt Checks (Play Mode)** checks fresh-press confirmation, indefinite reading, combat-input blocking/restoration, reach-state pacing, completion/disable cleanup and text fit. It captures `/tmp/hmb-tutorial-reading.png` and `/tmp/hmb-tutorial-practice.png`. These are prompt integration checks; they do not automate a full enemy-combat tutorial playthrough.
