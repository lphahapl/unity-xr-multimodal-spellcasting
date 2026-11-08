using System;
using System.Collections.Generic;
using UnityEngine;

namespace NarutoVR.SealCombat.Rpg
{
    // One course can have alternative casts. Its ordered seals describe a single route.
    internal sealed class WristTeachingLesson
    {
        public readonly string Name;
        public readonly SkillDefinition Gesture, Voice;
        public readonly IReadOnlyList<string> Seals;
        public WristTeachingLesson(string name, SkillDefinition gesture, SkillDefinition voice, IReadOnlyList<string> seals)
        {
            Name = name; Gesture = gesture; Voice = voice; Seals = seals ?? Array.Empty<string>();
        }
        public bool HasAlternative => Gesture && Voice && Gesture != Voice;
        public SkillDefinition Reference => Gesture ? Gesture : Voice;
    }

    internal sealed class WristTeachingLibrary
    {
        readonly List<WristTeachingLesson> courses = new();
        public int CourseCount => courses.Count;
        LinearRpgController source;
        int revision = -1;
        IReadOnlyList<SkillDefinition> recipes;

        public void Sync(LinearRpgController owner)
        {
            var definitions = owner && owner.skills ? owner.skills.ConfiguredSkills : null;
            int nextRevision = owner ? owner.TechniqueRevision : -1;
            if (source == owner && revision == nextRevision && ReferenceEquals(recipes, definitions)) return;
            source = owner; revision = nextRevision; recipes = definitions; courses.Clear();
            if (recipes == null) return;
            var curriculum = owner.openingChapter ? owner.openingChapter.techniques : null;
            if (curriculum)
            {
                foreach (var course in curriculum.OrderedCourses)
                {
                    if (course?.skillIds == null) continue;
                    SkillDefinition gesture = null, voice = null;
                    foreach (var id in course.skillIds)
                    {
                        if (!owner.IsTechniqueLearned(id)) continue;
                        foreach (var skill in recipes)
                        {
                            if (!skill || skill.skillId != id) continue;
                            if (skill.triggerMode != SkillTriggerMode.VoiceOnly && !gesture) gesture = skill;
                            if (skill.triggerMode != SkillTriggerMode.GestureOnly && !voice) voice = skill;
                            break;
                        }
                    }
                    Add(course.displayName, gesture, voice);
                }
                return;
            }
            foreach (var skill in recipes)
            {
                if (!skill || (owner.openingChapter && !owner.IsTechniqueLearned(skill.skillId))) continue;
                Add(skill.displayName, skill.triggerMode == SkillTriggerMode.VoiceOnly ? null : skill,
                    skill.triggerMode == SkillTriggerMode.GestureOnly ? null : skill);
            }
        }

        void Add(string name, SkillDefinition gesture, SkillDefinition voice)
        {
            if (!gesture && !voice) return;
            // SkillDefinition's current matcher accepts exactly one seal. Do not turn an alternative cast into a sequence.
            var order = gesture && !string.IsNullOrWhiteSpace(gesture.sealName) ? new[] { gesture.sealName } : Array.Empty<string>();
            courses.Add(new WristTeachingLesson(name, gesture, voice, order));
        }

        public int PageCount(bool voiceIndex)
        {
            int count = 0;
            foreach (var course in courses)
                count += voiceIndex ? (course.Voice ? 1 : 0) : Math.Max(1, course.Seals.Count);
            return count;
        }

        public WristTeachingLesson PageAt(int page, bool voiceIndex, out int sealIndex)
        {
            sealIndex = 0;
            foreach (var course in courses)
            {
                int size = voiceIndex ? (course.Voice ? 1 : 0) : Math.Max(1, course.Seals.Count);
                if (page < size) { sealIndex = page; return course; }
                page -= size;
            }
            return null;
        }
    }
}
