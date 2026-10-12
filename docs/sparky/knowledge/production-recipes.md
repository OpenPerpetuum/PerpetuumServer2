# Production recipes

A direct recipe lists the components consumed for one production cycle and the number of finished items that cycle produces. A component can itself be a craftable item.

A full-material list recursively expands craftable components until it reaches materials without another recipe, then combines repeated materials. It answers a different question from the direct recipe. If any required component or translation is unavailable, the list must be reported as incomplete rather than presented as the full cost.

The displayed recipe quantities describe the recipe data. They do not include player skills, facility efficiency, production mode, market price, or a NIC cost unless those values are separately available and explicitly calculated.
