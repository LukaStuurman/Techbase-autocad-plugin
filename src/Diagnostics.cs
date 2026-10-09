using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace Techbase.Blocks;

internal static class Diagnostics
{
    internal static void Run(Catalog catalog)
    {
        var output = Environment.GetEnvironmentVariable("TECHBASE_TEST_OUTPUT") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Techbase", "Blocks", "Validation");
        Directory.CreateDirectory(output);
        BlockVersionDiagnostics.Run(catalog, output);
        RotationSettingsDiagnostics.Run(catalog, output);
        PlacementDiagnostics.Run(catalog, output);
        TypographyDiagnostics.Run(catalog, output);
        BlockLayerDiagnostics.Run(catalog, output);
        var checks = new List<string>();
        var results = new List<object>();
        void Check(bool condition, string name) { if (!condition) throw new InvalidOperationException("Test mislukt: " + name); checks.Add(name); }
        using var db = new Database(true, true);
        db.DisableUndoRecording(false);
        using var working = new WorkingDatabaseScope(db);
        using (var tx = db.TransactionManager.StartTransaction())
        {
            var layerA = BlockInserter.DefaultLayer(db, tx, "TEST_A");
            var layerB = BlockInserter.DefaultLayer(db, tx, "TEST_B");
            var space = (BlockTableRecord)tx.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
            void Add(Entity entity, ObjectId layer) { entity.LayerId = layer; space.AppendEntity(entity); tx.AddNewlyCreatedDBObject(entity, true); }
            Add(new Line(new Point3d(-10, 0, 0), new Point3d(10, 0, 0)), layerA);
            Add(new Line(new Point3d(0, -10, 0), new Point3d(0, 10, 0)), layerB);
            var polyline = new Polyline(); polyline.AddVertexAt(0, new Point2d(20, 0), 0, 0, 0); polyline.AddVertexAt(1, new Point2d(30, 0), 0, 0, 0); Add(polyline, layerB);
            Add(new Circle(new Point3d(50, 0, 0), Vector3d.ZAxis, 2), layerA);
            Add(new Line(new Point3d(20, 0, 0), new Point3d(30, 0, 0)), layerB);
            Check(LayerResolver.Resolve(db, tx, new Point3d(5, 0, 0), 0.05).LayerId == layerA, "lijnlaag");
            Check(LayerResolver.Resolve(db, tx, Point3d.Origin, 0.05).Ambiguous, "kruising verschillende lagen vraagt keuze");
            Check(LayerResolver.Resolve(db, tx, new Point3d(25, 0.04, 0), 0.05).LayerId == layerB, "polyline binnen tolerantie + dubbele gelijke laag");
            Check(LayerResolver.Resolve(db, tx, new Point3d(25, 0.06, 0), 0.05).LayerId.IsNull, "buiten tolerantie");
            Check(LayerResolver.Resolve(db, tx, new Point3d(52, 0, 0), 0.05).LayerId == layerA, "cirkel");
            Check(LayerResolver.Resolve(db, tx, new Point3d(50, 0, 0), 0.05).LayerId.IsNull, "cirkelmiddelpunt is geen lijncontact");
            Check(LayerResolver.Resolve(db, tx, new Point3d(5, 0, 1), 0.05).LayerId.IsNull, "3D afstand gebruikt");
            var locked = (LayerTableRecord)tx.GetObject(layerA, OpenMode.ForWrite); locked.IsLocked = true;
            var rejected = false;
            try { LayerResolver.Resolve(db, tx, new Point3d(5, 0, 0), 0.05); } catch (InvalidOperationException) { rejected = true; }
            Check(rejected, "vergrendelde laag beschermd"); locked.IsLocked = false;
            var text = new DBText { TextString = "Objectlaag", Position = new Point3d(80, 0, 0), Height = 1 }; Add(text, layerA);
            Check(LayerResolver.Resolve(db, tx, text.Position, 0.05, new[] { text.ObjectId }).LayerId == layerA, "tekst via werkelijke cursorselectie");
            tx.Commit();
        }
        // Cancelling must not leave an insertion entity in the drawing.
        var first = catalog.Tools.First();
        int beforeCancel;
        using (var tx = db.TransactionManager.StartTransaction())
            beforeCancel = ((BlockTableRecord)tx.GetObject(db.CurrentSpaceId, OpenMode.ForRead)).Cast<ObjectId>().Count(id => !id.IsErased);
        using (var tx = db.TransactionManager.StartTransaction())
        {
            var definition = BlockInserter.Import(db, catalog, first);
            var layer = BlockInserter.DefaultLayer(db, tx, "TEST_CANCEL");
            using var block = BlockInserter.CreateReference(db, tx, definition, first, new Point3d(90, 90, 0), layer, Matrix3d.Identity);
            BlockInserter.Append(db, tx, block, false);
        }
        using (var tx = db.TransactionManager.StartTransaction())
        {
            Check(((BlockTableRecord)tx.GetObject(db.CurrentSpaceId, OpenMode.ForRead)).Cast<ObjectId>().Count(id => !id.IsErased) == beforeCancel, "annuleren laat geen blockreferentie achter");
        }
        var handles = new List<(string Handle, string Layer, string Block, bool Dynamic, int Attributes)>();
        var originalLayer = db.Clayer;
        for (var index = 0; index < catalog.Tools.Count; index++)
        {
            var tool = catalog.Tools[index];
            using var tx = db.TransactionManager.StartTransaction();
            var id = BlockInserter.Import(db, catalog, tool);
            var layer = BlockInserter.DefaultLayer(db, tx, "TEST_INSERT");
            using var block = BlockInserter.CreateReference(db, tx, id, tool, new Point3d(index % 8 * 50, 100 + index / 8 * 50, 0), layer, Matrix3d.Identity);
            var definition = (BlockTableRecord)tx.GetObject(id, OpenMode.ForRead);
            var expectedAttributes = definition.Cast<ObjectId>().Select(oid => tx.GetObject(oid, OpenMode.ForRead)).OfType<AttributeDefinition>().Count(a => !a.Constant);
            var entities = BlockInserter.Append(db, tx, block, tool.Explode);
            Check(entities.Count > 0, "plaatsing " + tool.Name);
            if (!tool.Explode)
            {
                Check(block.AttributeCollection.Count == expectedAttributes, "attributen " + tool.Name);
                Check(block.LayerId == layer, "invoeglaag " + tool.Name);
                Check(Math.Abs(block.ScaleFactors.X - tool.Scale) < 1e-10, "paletteschaal " + tool.Name);
                handles.Add((block.Handle.ToString(), block.Layer, tool.BlockName, block.IsDynamicBlock, expectedAttributes));
                if (block.IsDynamicBlock)
                {
                    // Some source blocks have dynamic actions without exposed editable properties.
                    var writable = block.DynamicBlockReferencePropertyCollection.Cast<DynamicBlockReferenceProperty>()
                        .FirstOrDefault(p => !p.ReadOnly && p.GetAllowedValues().Length > 1);
                    if (writable != null)
                    {
                        var value = writable.GetAllowedValues().Last(); writable.Value = value;
                        Check(Equals(writable.Value, value), "dynamische variant wisselen " + tool.Name);
                    }
                }
            }
            else Check(block.IsErased, "explode verwijdert blockreferentie " + tool.Name);
            results.Add(new { tool.Name, tool.BlockName, tool.Group, tool.Scale, tool.Explode, Dynamic = definition.IsDynamicBlock, Entities = entities.Count });
            tx.Commit();
        }
        Check(db.Clayer == originalLayer, "huidige tekenlaag ongewijzigd");
        db.SaveAs(Path.Combine(output, "Alle blocks - controle.dwg"), DwgVersion.AC1032);
        db.DxfOut(Path.Combine(output, "Alle blocks - controle.dxf"), 16, DwgVersion.AC1032);
        using (var reopened = new Database(false, true))
        {
            reopened.ReadDwgFile(Path.Combine(output, "Alle blocks - controle.dwg"), FileOpenMode.OpenForReadAndAllShare, true, null); reopened.CloseInput(true);
            using var tx = reopened.TransactionManager.StartTransaction();
            foreach (var item in handles)
            {
                var id = reopened.GetObjectId(false, new Handle(Convert.ToInt64(item.Handle, 16)), 0);
                var block = (BlockReference)tx.GetObject(id, OpenMode.ForRead);
                Check(block.Layer == item.Layer, "opgeslagen laag " + item.Handle);
                Check(block.IsDynamicBlock == item.Dynamic, "opgeslagen dynamisch block " + item.Handle);
                Check(block.AttributeCollection.Count == item.Attributes, "opgeslagen attributen " + item.Handle);
            }
        }
        var report = new { Status = "PASS", Host = Convert.ToString(AcApp.GetSystemVariable("ACADVER")), Timestamp = DateTimeOffset.Now, Palette = catalog.PalettePath, ToolCount = catalog.Tools.Count, Checks = checks, Blocks = results };
        File.WriteAllText(Path.Combine(output, "validatie.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        AcApp.DocumentManager.MdiActiveDocument?.Editor.WriteMessage("\nTECHBASE VALIDATION PASS: " + checks.Count + " checks, " + catalog.Tools.Count + " tools. " + output);
    }
}
