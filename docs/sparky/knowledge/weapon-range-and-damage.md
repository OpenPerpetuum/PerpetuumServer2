# Weapon range and damage

Weapon modules use an optimal range and a falloff range. Within optimal range, distance does not reduce the range-based damage factor. Between optimal range and the end of falloff, that factor decreases smoothly. Beyond the combined range, ordinary weapon damage is zero.

The ammunition supplies the weapon's damage types and base damage values. The weapon and ammunition can modify those values, so the item and ammunition combination matters. A weapon's displayed accuracy, range, cycle time, and an ammunition's damage are separate facts; do not infer one from another.

Individual attacks can vary. The server applies a small random damage variation and can apply a critical hit based on the attacker's critical-hit chance. These effects mean a displayed base value is not a guarantee of the exact damage from one shot.
