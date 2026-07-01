# Player Movement Script

This document explains the settings exposed by the **Player Movement Script** and how each one affects gameplay feel.

---

## Input

### Use Rewired Input

Enables the script to read input directly from **Rewired** every `Update()`.

Keep this enabled for normal gameplay.

### Rewired Player Id

Defines which Rewired player this script reads from.

This must match your Rewired player ID.

> In the current setup, this appeared to be `1`, not `0`.

### Horizontal Action

The Rewired axis name used for left and right movement.

This must exactly match the action name configured in Rewired.

> Current setup appeared to use: `Horizontal Axis`

### Vertical Action

The Rewired axis name used for up and down input.

Used for:

* Dash direction
* Fast-fall input

> Current setup appeared to use: `Vertical Axis`

### Jump Action

The Rewired button name used for jumping.

Pressing jump buffers a jump. Holding jump affects jump height.

### Dash Action

The Rewired button name used for dashing.

---

## Collision

### Ground Layer

Defines which layers are treated as ground.

Your player must **not** be on this layer, or the ground check may detect the player itself.

Set this only to layers used by:

* Platforms
* Tilemaps
* Ground objects

### Ground Check Distance

Defines how far below the collider the script checks for ground.

Increase this value if:

* Grounded state flickers
* Coyote time never refills
* The player sometimes fails to detect the floor

Recommended range:

```text
0.03 to 0.08
```

Keep this value small.

---

## Horizontal Movement

### Max Run Speed

The player’s top horizontal speed.

Higher values make the player run faster.

Recommended range for snappier platforming:

```text
5.5 to 8
```

### Run Acceleration

Controls how quickly the player reaches max speed.

Higher values make movement more instant and responsive.

Increase this if movement feels sluggish.

Lower this if movement feels too sharp.

### Run Deceleration

Controls how quickly the player slows down when no horizontal input is held.

Higher values mean less sliding.

Lower values create a floatier or skiddier feeling.

### Air Control Multiplier

Controls how much horizontal control the player has while in the air.

```text
1.0 = same control as on the ground
0.5 = weaker air steering
```

For a Celeste-like feel, strong but not full air control usually works well.

Recommended range:

```text
0.6 to 0.9
```

### Input Dead Zone

Ignores small input values below this threshold.

Useful for controller sticks.

Lower this if analog movement feels unresponsive.

Raise this if stick drift moves the player.

---

## Jumping

### Jump Speed

The initial upward velocity applied when jumping.

Higher values create taller and faster jumps.

### Gravity

Controls how strongly the player is pulled downward.

Higher values create:

* Sharper jump arcs
* Faster falling
* Less floaty movement

If jumps feel floaty, increase gravity.

If jumps feel too heavy, lower gravity.

### Half Gravity Threshold

When the player is holding jump and rising slowly, gravity is reduced if the vertical speed is below this value.

This creates a softer jump apex.

Higher values create more float near the top of the jump.

### Jump Cut Multiplier

When the player releases jump early, the upward velocity is multiplied by this value.

Lower values create a stronger short-hop effect.

Examples:

```text
0.3 = cuts the jump strongly
0.8 = barely cuts the jump
```

### Variable Jump Duration

Defines how long holding jump can influence jump height.

Higher values make holding jump matter for longer.

Lower values create a more fixed jump height.

### Max Fall Speed

The normal terminal fall speed.

Higher values make the player fall faster.

### Fast Fall Speed

The terminal fall speed when pressing down.

This should usually be higher than `Max Fall Speed`.

### Fast Fall Acceleration

Controls how quickly the player accelerates into a fast fall after already falling.

Higher values make down input snap faster into a fast fall.

### Coyote Time

Defines how long after walking off a ledge the player can still jump.

Recommended range:

```text
0.08 to 0.12
```

Set to `0` to disable coyote time.

### Jump Buffer Time

Defines how early before landing a jump press is remembered.

If the player presses jump just before touching the ground, this allows the jump to happen immediately on landing.

Recommended range:

```text
0.08 to 0.12
```

### Extra Air Jumps

Defines how many jumps are allowed after leaving the ground.

```text
0 = no double jump
1 = double jump
2 = triple jump
```

---

## Dash

### Dash Speed

The velocity applied during a dash.

Higher values make the dash faster and usually longer.

### Dash Duration

Defines how long the dash keeps full dash speed.

Dash distance is roughly:

```text
Dash Speed × Dash Duration
```

Increase duration for a longer dash.

Increase speed for a sharper dash.

### Dash Cooldown

The minimum time required before another dash can start.

Lower values feel more responsive.

Higher values prevent dash spam.

### Dash End Speed

The velocity kept after the dash ends.

Higher values preserve momentum.

Lower values make the dash stop more abruptly.

### Upward Dash End Multiplier

If the player dashes upward, this value scales the vertical speed after the dash ends.

Lower values prevent upward dashes from launching the player too high.

Example:

```text
0.75 = keeps 75% of the upward dash exit speed
```

### Dash Buffer Time

Defines how early a dash press is remembered before a dash becomes available.

Useful when the player presses dash slightly before touching the ground or refilling dash.

---

## Runtime Data

These values are read-only debugging values shown during Play Mode.

### Move Input

Shows what input the script is currently receiving.

If this stays at:

```text
(0, 0)
```

while pressing movement keys, the input configuration is probably wrong.

### Velocity

Shows the actual movement velocity currently being applied.

### Dash Direction

Shows the current or last dash direction.

### Timers

These values show grace windows counting down:

* Coyote Timer
* Jump Buffer Timer
* Dash Buffer Timer

Use these to confirm whether jump and dash timing windows are working correctly.

### Air Jumps Remaining

Shows how many air jumps the player currently has available.

Use this to confirm whether landing properly refills air jumps.

### Dashes Remaining

Shows how many dashes the player currently has available.

Use this to confirm whether dash refills are working correctly.

### Is Grounded

The most important debug flag.

If this never becomes `true`, jumps and dash refills may feel broken.

Check these first:

* Ground Layer
* Player collider setup
* Ground collider setup
* Ground Check Distance
