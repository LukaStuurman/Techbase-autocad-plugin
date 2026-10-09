using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace Techbase.Blocks;

internal static class BlockBurst
{
    internal static List<ObjectId> Append(Database db, Transaction tx, BlockReference block)
    {
        var result = new List<ObjectId>();
        var space = (BlockTableRecord)tx.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
        void Add(Entity entity)
        {
            if (entity.Layer == "0") entity.LayerId = block.LayerId;
            if (entity.Color.ColorMethod == Autodesk.AutoCAD.Colors.ColorMethod.ByBlock)
                entity.Color = block.Color.ColorMethod == Autodesk.AutoCAD.Colors.ColorMethod.ByLayer
                    ? ((LayerTableRecord)tx.GetObject(block.LayerId, OpenMode.ForRead)).Color : block.Color;
            result.Add(space.AppendEntity(entity)); tx.AddNewlyCreatedDBObject(entity, true);
            if (entity is DBText text) text.AdjustAlignment(db);
        }
        foreach (ObjectId id in block.AttributeCollection)
        {
            var attribute = (AttributeReference)tx.GetObject(id, OpenMode.ForRead);
            if (!attribute.Invisible) Add(ToText(db, attribute));
        }
        using var parts = new DBObjectCollection(); block.Explode(parts);
        foreach (DBObject part in parts)
        {
            if (part is AttributeDefinition definition)
            {
                if (definition.Constant && !definition.Invisible)
                {
                    using var attribute = new AttributeReference(); attribute.SetDatabaseDefaults(db);
                    // Exploded definitions already carry their world geometry.
                    attribute.SetAttributeFromBlock(definition, Matrix3d.Identity); attribute.TextString = definition.TextString;
                    attribute.AdjustAlignment(db); if (attribute.IsMTextAttribute) attribute.UpdateMTextAttribute();
                    Add(ToText(db, attribute));
                }
                definition.Dispose();
            }
            else if (part is Entity entity) Add(entity);
            else part.Dispose();
        }
        block.Erase(); return result;
    }
    private static Entity ToText(Database db, AttributeReference attribute)
    {
        Entity result;
        if (attribute.IsMTextAttribute) result = attribute.MTextAttribute;
        else
        {
            var text = new DBText(); text.SetDatabaseDefaults(db);
            text.TextString = attribute.TextString; text.TextStyleId = attribute.TextStyleId;
            text.Normal = attribute.Normal; text.Height = attribute.Height; text.Rotation = attribute.Rotation;
            text.WidthFactor = attribute.WidthFactor; text.Oblique = attribute.Oblique;
            text.HorizontalMode = attribute.HorizontalMode; text.VerticalMode = attribute.VerticalMode;
            text.Position = attribute.Position;
            if (!attribute.IsDefaultAlignment) text.AlignmentPoint = attribute.AlignmentPoint;
            text.IsMirroredInX = attribute.IsMirroredInX; text.IsMirroredInY = attribute.IsMirroredInY; text.Thickness = attribute.Thickness;
            result = text;
        }
        result.LayerId = attribute.LayerId; result.Color = attribute.Color;
        result.LinetypeId = attribute.LinetypeId; result.LinetypeScale = attribute.LinetypeScale;
        result.LineWeight = attribute.LineWeight; result.Transparency = attribute.Transparency;
        return result;
    }
}
