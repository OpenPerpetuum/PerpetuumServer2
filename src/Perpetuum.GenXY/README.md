# Perpetuum.GenXY

This project contains the platform-neutral GenXY reader, writer, and value
converter used by both the game server and administration tools.

The base codec handles primitive, collection, dictionary, date, color, and point
tokens without depending on the server assembly. Server-owned value types such
as `Position` and `Area` register their converters and readers from the
`Perpetuum` assembly at module initialization. Keep future server-specific token
support in that registration layer so this library remains portable.
