-- Optional: register POS order type in MealDB Enums if OrderTypeId references a lookup table.
-- Execute manually in MealDB. Adjust EnumTypeId to match your OrderType enum group.

/*

INSERT INTO Enums (Id, EnumTypeId, EnumValue, Description, IsActive, IsDeletable, IsEditable, CreatedBy, CreatedOn, SortOrder)
VALUES (78, 7, 'POS Order', 'POS Order', 1,0,0,1,GETDATE(),6);

*/
