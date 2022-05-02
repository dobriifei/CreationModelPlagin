using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

  namespace CreationModelPlagin
{
    [TransactionAttribute(TransactionMode.Manual)]
    public class CreationModel : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Document doc = commandData.Application.ActiveUIDocument.Document;
            List<Level> listLevel = GetLevels(doc);
            double width = UnitUtils.ConvertToInternalUnits(10000, UnitTypeId.Millimeters);
            double depth = UnitUtils.ConvertToInternalUnits(5000, UnitTypeId.Millimeters);
            List<XYZ> points = SetPoints(width, depth);
            List<Wall> walls = CreateWalls(doc, listLevel, points);
            AddDoor(doc, listLevel, walls[0]);
            AddWindows(doc, listLevel, walls);
            AddRoof(doc, listLevel, walls);
            return Result.Succeeded;
        }
        private void AddRoof(Document doc, List<Level> listLevel, List<Wall> walls)
        {
            var level = listLevel
                .Where(x => x.Name.Equals("Уровень 2"))
                .FirstOrDefault();
            RoofType roofType = new FilteredElementCollector(doc)
                .OfClass(typeof(RoofType))
                .OfType<RoofType>()
                .Where(x => x.Name.Equals("Типовой - 400мм"))
                .Where(x => x.FamilyName.Equals("Базовая крыша"))
                .FirstOrDefault();
            double wallWidth = walls[0].Width;
            double df = wallWidth / 2;
            double dh = level.get_Parameter(BuiltInParameter.LEVEL_ELEV).AsDouble();
            XYZ dt = new XYZ(-df, -df, dh);
            XYZ dz = new XYZ(0, 0, 20);
            XYZ dy = new XYZ(0, 20, 0);
            LocationCurve locationCurve = walls[0].Location as LocationCurve;
            XYZ point = locationCurve.Curve.GetEndPoint(0);
            double l = (walls[0].Location as LocationCurve).Curve.Length + df * 2;
            double w = ((walls[1].Location as LocationCurve).Curve.Length / 2) + df;
            XYZ origin = point + dt;
            XYZ vy = XYZ.BasisY;
            XYZ vz = XYZ.BasisZ;
            CurveArray curve = new CurveArray();
            curve.Append(Line.CreateBound(origin, origin + new XYZ(0, w, 5)));
            curve.Append(Line.CreateBound(origin + new XYZ(0, w, 5), origin + new XYZ(0, w * 2, 0)));
            var av = doc.ActiveView;
            Transaction transaction = new Transaction(doc, "Создание крыши");
            transaction.Start();
            ReferencePlane plane = doc.Create.NewReferencePlane2(origin, origin - vz, origin + vy, av);
            ExtrusionRoof extrusionRoof = doc.Create.NewExtrusionRoof(curve, plane, level, roofType, 0, l);
            transaction.Commit();

        }
        private void AddWindows(Document doc, List<Level> listLevel, List<Wall> walls)
        {
            var level = listLevel
                .Where(x => x.Name.Equals("Уровень 1"))
                .FirstOrDefault();
            FamilySymbol windowType = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_Windows)
                .OfType<FamilySymbol>()
                .Where(x => x.Name.Equals("0406 x 0610 мм"))
                .Where(x => x.FamilyName.Equals("Фиксированные"))
                .FirstOrDefault();
            Transaction transaction = new Transaction(doc, "Вставка окон ");
            transaction.Start();
            if (!windowType.IsActive)
            {
                windowType.Activate();
            }
            for (int i = 1; i < 4; i++)
            {
                Wall wall = walls[i];
                LocationCurve hostCurve = wall.Location as LocationCurve;
                XYZ point1 = hostCurve.Curve.GetEndPoint(0);
                XYZ point2 = hostCurve.Curve.GetEndPoint(1);
                XYZ point = (point1 + point2) / 2;
                var window = doc.Create.NewFamilyInstance(point, windowType, wall, level, StructuralType.NonStructural);
                window.get_Parameter(BuiltInParameter.INSTANCE_SILL_HEIGHT_PARAM).Set(UnitUtils.ConvertToInternalUnits(850, UnitTypeId.Millimeters));
            }
            transaction.Commit();
        }
        public void AddDoor(Document doc, List<Level> listLevel, Wall wall)
        {
            var level = listLevel
                .Where(x => x.Name.Equals("Уровень 1"))
                .FirstOrDefault();
            FamilySymbol doorType = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_Doors)
                .OfType<FamilySymbol>()
                .Where(x => x.Name.Equals("0915 x 2134 мм"))
                .Where(x => x.FamilyName.Equals("Одиночные-Щитовые"))
                .FirstOrDefault();
            LocationCurve hostCurve = wall.Location as LocationCurve;
            XYZ point1 = hostCurve.Curve.GetEndPoint(0);
            XYZ point2 = hostCurve.Curve.GetEndPoint(1);
            XYZ point = (point1 + point2) / 2;
            Transaction transaction = new Transaction(doc, "Вставка двери");
            transaction.Start();
            if (!doorType.IsActive)
            {
                doorType.Activate();
            }
            doc.Create.NewFamilyInstance(point, doorType, wall, level, StructuralType.NonStructural);
            transaction.Commit();
        }
        public List<Level> GetLevels(Document doc)
        {
            List<Level> listLevel = new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .OfType<Level>()
                .ToList();
            return listLevel;
        }
        public List<XYZ> SetPoints(double width, double depth)
        {
            double dx = width / 2;
            double dy = depth / 2;
            List<XYZ> points = new List<XYZ>();
            points.Add(new XYZ(-dx, -dy, 0));
            points.Add(new XYZ(dx, -dy, 0));
            points.Add(new XYZ(dx, dy, 0));
            points.Add(new XYZ(-dx, dy, 0));
            points.Add(new XYZ(-dx, -dy, 0));
            return (points);
        }

        public List<Wall> CreateWalls(Document doc, List<Level> listLevel, List<XYZ> points)
        {
            var level1 = listLevel
                .Where(x => x.Name.Equals("Уровень 1"))
                .FirstOrDefault();
            var level2 = listLevel
                .Where(x => x.Name.Equals("Уровень 2"))
                .FirstOrDefault();
            List<Wall> walls = new List<Wall>();
            Transaction transaction = new Transaction(doc, "Создание стен");
            transaction.Start();
            for (int i = 0; i < 4; i++)
            {
                Line line = Line.CreateBound(points[i], points[i + 1]);
                Wall wall = Wall.Create(doc, line, level1.Id, false);
                walls.Add(wall);
                wall.get_Parameter(BuiltInParameter.WALL_HEIGHT_TYPE).Set(level2.Id);
            }
            transaction.Commit();
            return (walls);
        }
    }
} 
