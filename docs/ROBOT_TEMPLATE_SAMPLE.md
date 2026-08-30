# Sample formats

## Template Genxy string format

#robot=i158C#head=i158D#chassis=i158E#leg=i158F#container=i14A#headModules=[|m0=[|definition=i302|slot=i1]|m1=[|definition=i302|slot=i2]|m2=[|definition=i314|slot=i3]|m3=[|definition=i3A7|slot=i4]|m4=[|definition=i3A7|slot=i5]]#chassisModules=[|m0=[|definition=i34D|slot=i1|ammoDefinition=i986|ammoQuantity=i17]|m1=[|definition=i34D|slot=i2|ammoDefinition=i986|ammoQuantity=i17]|m2=[|definition=i34D|slot=i3|ammoDefinition=i986|ammoQuantity=i17]|m3=[|definition=i34D|slot=i4|ammoDefinition=i986|ammoQuantity=i17]]#legModules=[|m0=[|definition=i2BA|slot=i1]|m1=[|definition=i33B|slot=i2|ammoDefinition=i298|ammoQuantity=i8]|m2=[|definition=i3BC|slot=i2]|m3=[|definition=i3BC|slot=i4]|m4=[|definition=i2B1|slot=i5]]

## Robot part options format

#height=f0.45#slotFlags=4451,6d1,451,6d3

# Description

- Robot consists of 4 parts: head, chassis, leg, container. Each part has a definition (e.g. i158C) and a set of modules. Each module has a definition, a slot number, and optionally ammo definition+quantity.
- The Genxy string encodes all of this in a compact form. The editor will decode it into a structured form for editing, then re-encode on save.
- The editor will also show the translated names for each definition using the `TranslationService` from Phase 2, so you see human-readable labels instead of just "i158C".
- The editor will allow adding/removing modules, changing definitions, and editing ammo quantities. Each change will produce an `IPendingChange` that can be applied directly to the DB or exported as SQL.
- The loot editor will show the items contained in a robot's container, allowing you to add/remove items and change quantities. This also produces `IPendingChange` entries for DB/script.
- This template is just a sample to illustrate the format. The actual editor will have a more user-friendly UI with dropdowns for definitions, drag-and-drop module assignment, and real-time translation lookups.
- The goal is to make it easy for admins to create complex robot configurations without having to manually write Genxy strings or SQL statements. The tool will handle all the encoding/decoding and provide a visual interface for managing robot templates and their loot.
- The editor will also validate changes before applying them, ensuring that module definitions are compatible with their assigned slots and that ammo quantities are within allowed limits. This helps prevent errors and ensures that the resulting robot configurations are valid within the game's rules.
- Overall, this tool will streamline the process of managing robot templates and loot tables, making it more efficient and less error-prone for administrators to maintain the game world.
- Robot part options format is a compact way to encode the height and slot flags for a robot part. The height is a floating-point value representing the part's height, while the slot flags are a comma-separated list of hexadecimal values representing the available slots for modules on that part. This format allows for easy parsing and editing of robot part configurations within the admin tool.