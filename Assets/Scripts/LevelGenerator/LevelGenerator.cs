using System.Collections.Generic;
using System.Linq;
using LvlGen.Scripts.Exceptions;
using LvlGen.Scripts.Helpers;
using LvlGen.Scripts.Structure;
using UnityEngine;
using UnityEngine.UI;

namespace LvlGen.Scripts
{
    public class LevelGenerator : MonoBehaviour
    {
        /// <summary>
        /// LvlGen seed
        /// </summary>
        [SerializeField] private int Seed;

        /// <summary>
        /// Container for all sections in hierarchy
        /// </summary>
        [SerializeField] private Transform SectionContainer;

        /// <summary>
        /// Maximum level size measured in sections
        /// </summary>
        [SerializeField] private int MaxLevelSize;

        /// <summary>
        /// Maximum allowed distance from the original section
        /// </summary>
        public int MaxAllowedOrder;

        /// <summary>
        /// Spawnable section prefabs
        /// </summary>
        [SerializeField] private Section[] Sections;

        /// <summary>
        /// Spawnable dead ends
        /// </summary>
        public DeadEnd[] DeadEnds;

        /// <summary>
        /// Tags that will be taken into consideration when building the first section
        /// </summary>
        [SerializeField] private string[] InitialSectionTags;

        /// <summary>
        /// Special section rules, limits and forces the amount of a specific tag
        /// </summary>
        [SerializeField] private TagRule[] SpecialRules;

        private List<Section> registeredSections = new List<Section>();

        [SerializeField] private Object playerModel;

        public int LevelSize { get; private set; }
        public Transform Container => SectionContainer != null ? SectionContainer : transform;

        protected IEnumerable<Collider> RegisteredColliders => RegisteredSections.SelectMany(s => s.Bounds.Colliders).Union(DeadEndColliders);
        protected List<Collider> DeadEndColliders = new List<Collider>();
        protected bool HalfLevelBuilt => RegisteredSections.Count > LevelSize;

        protected List<Section> RegisteredSections { get => registeredSections; }

        public void StartGeneration()
        {
            if (Seed != 0)
                RandomService.SetSeed(Seed);
            else
                Seed = RandomService.Seed;

            CheckRuleIntegrity();
            LevelSize = MaxLevelSize;
            CreateInitialSection();
            DeactivateBounds();
        }


        protected void CheckRuleIntegrity()
        {
            foreach (var ruleTag in SpecialRules.Select(r => r.Tag))
            {
                if (SpecialRules.Count(r => r.Tag.Equals(ruleTag)) > 1)
                    throw new InvalidRuleDeclarationException();
            }
        }

        public Section GetSpawnRoom() { return RegisteredSections.Find(section => section.Tags[0].Equals("spawn")); }

        protected void CreateInitialSection() => Instantiate(PickSectionWithTag(InitialSectionTags), transform).Initialize(this, 0);

        public void AddSectionTemplate() => Instantiate(Resources.Load("SectionTemplate"), Vector3.zero, Quaternion.identity);
        public void AddDeadEndTemplate() => Instantiate(Resources.Load("DeadEndTemplate"), Vector3.zero, Quaternion.identity);

        public bool IsSectionValid(Bounds newSection, Bounds sectionToIgnore) =>
            !RegisteredColliders.Except(sectionToIgnore.Colliders).Any(c => c.bounds.Intersects(newSection.Colliders.First().bounds));

        public void RegisterNewSection(Section newSection)
        {
            RegisteredSections.Add(newSection);

            if (SpecialRules.Any(r => newSection.Tags.Contains(r.Tag)))
                SpecialRules.First(r => newSection.Tags.Contains(r.Tag)).PlaceRuleSection();

            LevelSize--;
        }

        public void RegistrerNewDeadEnd(IEnumerable<Collider> colliders) => DeadEndColliders.AddRange(colliders);

        public Section PickSectionWithTag(string[] tags)
        {
            if (RulesContainTargetTags(tags) && HalfLevelBuilt)
            {
                foreach (var rule in SpecialRules.Where(r => r.NotSatisfied))
                {
                    if (tags.Contains(rule.Tag))
                    {
                        return Sections.Where(x => x.Tags.Contains(rule.Tag)).
                            PickOne();
                    }
                }
            }

            var pickedTag = PickFromExcludedTags(tags);

            if (pickedTag != "")
                return Sections.Where(x => x.Tags.Contains(pickedTag)).PickOne();

            return null;
        }

        protected string PickFromExcludedTags(string[] tags)
        {
            var tagsToExclude = SpecialRules.Where(r => r.Completed).Select(rs => rs.Tag);

            if (tags.Except(tagsToExclude).Count() > 0)
                return tags.Except(tagsToExclude).PickOne();

            return "";
        }

        protected bool RulesContainTargetTags(string[] tags) => tags.Intersect(SpecialRules.Where(r => r.NotSatisfied).Select(r => r.Tag)).Any();

        protected void DeactivateBounds()
        {
            foreach (var c in RegisteredColliders)
                c.enabled = false;
        }


    }
}