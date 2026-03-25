Void Bastion — README
1. Game Overview

Genre:

Tower Defense + Resource Management + Casual Mobile Strategy

Platform:

Android (mobile-first)
Description

The player controls a living hole that collects resources and powers a castle. The castle automatically defends against waves of enemies, while the player balances between resource gathering and strengthening defenses.

Core Fantasy

You are not the hero — you are the source of power. The hole feeds your defenses, but poor decisions can lead to destruction.

Core Experience
One-hand, intuitive controls
Satisfying resource collection
Constant pressure from enemy waves
Strategic choice: gather vs defend
References
hole.io — hole control mechanic
Kingdom Rush — tower defense structure
Archero / Vampire Survivors — simplicity + escalating chaos
Clash Royale (PvE) — short sessions
2. Controls (Android)
Design Principles

Controls should be:

intuitive
playable with one hand
minimal in complexity
Core Controls
Tap & drag — move the hole
Auto-absorb — objects are absorbed when inside the radius
Sprint button — temporary speed boost (with cooldown)
Additional Notes
Sprint is placed in the bottom-right corner
All gameplay can be handled with one finger
UI Elements

Always visible on screen:

Castle HP
Current wave
Resources
Building Controls
Bottom panel with buttons:
Castle upgrade
Tower building
Hole upgrade
Tap to open menu
Tap to select upgrade
Rationale
Minimal input → maximum accessibility
Auto-absorb removes unnecessary actions
Fast decisions fit mobile gameplay
3. Core Mechanics
The Hole
Controlled by the player
Collects resources
Can absorb weak enemies

Parameters:

Radius
Pull strength
Movement speed
Resources
Collected from world objects (trees, rocks, etc.)
Automatically converted into currency

Used for:

Building
Upgrades
Castle
Central base
Automatically attacks enemies
Progresses from a small camp to a fortified stronghold
Enemy Waves
Follow a predefined path
Increase in strength over time
Create constant pressure
Core Tension

The player constantly chooses between:

Gathering resources (risk)
Supporting defense (safety)
4. MVP (First Playable Prototype)
Goal

Validate the core loop:

move → gather → upgrade → defend

MVP Requirements
Hole
Drag movement
Auto-absorption
Visual growth
Resources
Basic objects (e.g., trees)
Provide resources when absorbed
Castle
Single central structure
Automatic attack (e.g., shooting arrows)
Enemies
One enemy type
Follow a path
Damage the castle on contact
Upgrades

At least 2:

Castle upgrade (HP / damage)
Hole radius increase
Waves
Minimum 3 waves
Pause between waves
UI
Resource counter
Castle HP
Upgrade buttons
5. Definition of Done
Gameplay

The player can:

Move the hole
Collect resources
Spend resources on upgrades
Survive multiple waves
Core Loop Works

The player experiences:

gather → upgrade → defend → repeat

And:

understands it without explanation
feels tension and decision-making
Balance
Not gathering → lack of resources
Not defending → defeat
Fun Factor
Absorption feels satisfying
There are “close call” moments
Encourages replay
Session Length
5–10 minutes per session
Clear:
beginning
escalation
outcome (win/lose)
Usability
Fully playable with one hand
Clean, uncluttered UI
No tutorial required to understand basics
Post-MVP Development
New tower types
Multiple resource types
Enemy variations
Active hole abilities
Meta progression