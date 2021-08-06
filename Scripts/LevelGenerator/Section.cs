using System;
using System.Collections.Generic;
using System.Linq;
using LvlGen.Scripts.Helpers;
using UnityEngine;

namespace LvlGen.Scripts
{
    using Coord = Tuple<int, int>;

    [System.Serializable]
    public class Rows
    {
        public bool[] array;
    }


public class Section : MonoBehaviour
    {
        /// <summary>
        /// Section tags
        /// </summary>
        public string[] Tags;

        /// <summary>
        /// Tags that this section can annex
        /// </summary>
        public string[] CreatesTags;

        /// <summary>
        /// Exits node in hierarchy
        /// </summary>
        public Exits Exits;

        /// <summary>
        /// Bounds node in hierarchy
        /// </summary>
        public Bounds Bounds;

        /// <summary>
        /// Chances of the section spawning a dead end
        /// </summary>
        public int DeadEndChance;

        protected LevelGenerator LevelGenerator;
        protected int order;
        [SerializeField] private Rows[] tiles;
        [SerializeField] private int enemiesChance;
        [SerializeField] private int entranceRow;
        [SerializeField] private int entranceCol;
        [SerializeField] private int maxEnemiesAllowed;
        private bool isLocked = false;
        private bool isCleared = false;
        private bool hasEnemies;
        private Dictionary<Coord, Tuple<Section, Coord>> transfers = new Dictionary<Coord, Tuple<Section, Coord>>();
        private Direction orientation = Direction.None;


        public Rows[] Tiles { get => tiles; set => tiles = value; }
        public int EntranceRow { get => entranceRow; }
        public int EntranceCol { get => entranceCol; }
        public Dictionary<Coord, Tuple<Section, Coord>> Transfers { get => transfers; }
        public Direction Orientation { get => orientation; set => orientation = value; }
        public bool IsLocked { get => isLocked; set => isLocked = value; }
        public bool IsCleared { get => isCleared; set => isCleared = value; }
        public bool HasEnemies { get => hasEnemies; set => hasEnemies = value; }
        public int MaxEnemiesAllowed { get => maxEnemiesAllowed; }

        public void Initialize(LevelGenerator levelGenerator, int sourceOrder)
        {
            LevelGenerator = levelGenerator;
            transform.SetParent(LevelGenerator.Container);
            LevelGenerator.RegisterNewSection(this);
            order = sourceOrder + 1;

            if (RandomService.RollD100(enemiesChance)) hasEnemies = true;
            else hasEnemies = false;

            GenerateAnnexes();
        }

        protected void GenerateAnnexes()
        {
            if (CreatesTags.Any())
            {
                foreach (var e in Exits.ExitSpots)
                {
                    if (LevelGenerator.LevelSize > 0 && order < LevelGenerator.MaxAllowedOrder)
                        if (RandomService.RollD100(DeadEndChance))
                            PlaceDeadEnd(e);
                        else
                            GenerateSection(e);
                    else
                        PlaceDeadEnd(e);
                }
            }
        }

        protected void AddOrigin(Transform exit, Section origin)
        {
            Exit exitObject = exit.GetComponent<Exit>();
            Coord endCoordinates = new Coord(exitObject.Row, exitObject.Col);
            Coord startCoordinates = new Coord(this.entranceRow, this.entranceCol);
            Transfers[startCoordinates] = new Tuple<Section, Coord>(origin, endCoordinates);
        }

        protected void GenerateSection(Transform exit)
        {
            var candidate = IsAdvancedExit(exit)
                ? BuildSectionFromExit(exit.GetComponent<AdvancedExit>())
                : BuildSectionFromExit(exit);

            if (candidate != null)
            {
                if (LevelGenerator.IsSectionValid(candidate.Bounds, Bounds))
                {
                    candidate.Initialize(LevelGenerator, order);
                    Exit exitObject = exit.GetComponent<Exit>();
                    Coord startCoordinates = new Coord(exitObject.Row, exitObject.Col);
                    Coord endCoordinates = new Coord(candidate.EntranceRow, candidate.EntranceCol);
                    Transfers[startCoordinates] = new Tuple<Section, Coord>(candidate, endCoordinates);
                    candidate.AddOrigin(exit, this);
                }
                else
                {
                    Destroy(candidate.gameObject);
                    PlaceDeadEnd(exit);
                }
            }
        }

        protected void PlaceDeadEnd(Transform exit)
        {
            DeadEnd de = Instantiate(LevelGenerator.DeadEnds.PickOne(), exit);
            de.Initialize(LevelGenerator);
            de.transform.parent = exit.transform;

            Transform magicWall = exit.transform.Find("Magic Wall");

            if (magicWall != null) { magicWall.gameObject.SetActive(false); }
        }

        protected bool IsAdvancedExit(Transform exit) => exit.GetComponent<AdvancedExit>() != null;

        protected Section BuildSectionFromExit(Transform exit)
        {
            var newSection = LevelGenerator.PickSectionWithTag(CreatesTags);

            if (newSection == null)
            {
                PlaceDeadEnd(exit);
                return null;
            }

            return Instantiate(LevelGenerator.PickSectionWithTag(CreatesTags), exit).GetComponent<Section>();
        }

        protected Section BuildSectionFromExit(AdvancedExit exit) => Instantiate(LevelGenerator.PickSectionWithTag(exit.CreatesTags), exit.transform).GetComponent<Section>();

        
    }
}