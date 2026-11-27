using UnityEngine;

namespace NarutoVR.SealCombat.Rpg
{
    // Owned by the existing chapter bridge. Both landings use the deck's authored local space.
    internal sealed class Chapter02PassageCrossing
    {
        readonly Chapter02PassageStage stage;
        readonly PassageGlyphAnchor near, far;
        NarrationController voice;
        bool wasDrawing;
        internal PassageGlyphRoad Road => near.RoadTo(far);
        internal bool Joined
        {
            get
            {
                var road = Road;
                return road && road.Progress >= 1f;
            }
        }

        internal Chapter02PassageCrossing(Chapter02BridgeEquipment bridge, Chapter02PassageStage stage)
        {
            this.stage = stage;
            Transform deck = bridge.repairedDeck.transform;
            var root = new GameObject("断桥 · 通路符两岸").transform;
            root.SetParent(deck.parent, false);
            root.SetPositionAndRotation(deck.position, deck.rotation);
            root.localScale = deck.localScale;
            Vector3 forward = stage.farLanding - stage.nearLanding;
            near = Landing(root, "近岸铜榫", stage.nearLanding, forward, deck.gameObject.layer);
            far = Landing(root, "对岸铜榫", stage.farLanding, -forward, deck.gameObject.layer);
            bridge.brokenBarrier.AddComponent<PassageSightWindow>();
            // Keep the repaired state's quest fixtures; the spell supplies the only crossing floor.
            deck.GetComponent<MeshRenderer>().enabled = false;
            foreach (var slab in deck.GetComponents<BoxCollider>()) slab.enabled = false;
            bridge.panel.template = stage.passage;
        }

        PassageGlyphAnchor Landing(Transform parent, string name, Vector3 position, Vector3 direction, int layer)
        {
            var obj = new GameObject(name);
            obj.layer = layer;
            obj.transform.SetParent(parent, false);
            obj.transform.SetLocalPositionAndRotation(position, Quaternion.LookRotation(direction));
            var socket = Object.Instantiate(stage.socketPrefab, obj.transform, false);
            foreach (var shape in socket.GetComponentsInChildren<Collider>()) shape.enabled = false;
            PickBox(obj, new Vector3(0, .035f, 0), new Vector3(.94f, .10f, .58f));
            PickBox(obj, new Vector3(-.35f, .33f, -.125f), new Vector3(.26f, .57f, .17f));
            var anchor = obj.AddComponent<PassageGlyphAnchor>();
            anchor.bank = anchor.landing = obj.transform;
            return anchor;
        }

        static void PickBox(GameObject obj, Vector3 center, Vector3 size)
        {
            var shape = obj.AddComponent<BoxCollider>();
            shape.isTrigger = true;
            shape.center = center;
            shape.size = size;
        }

        internal void UpdateStage(bool drawing)
        {
            if (drawing && !wasDrawing) Speak(stage.drawingCue);
            if (!drawing && wasDrawing && voice) voice.StopDialogue();
            wasDrawing = drawing;
        }

        internal void TeachAim() => Speak(stage.aimingCue);

        void Speak(NarrationLine line)
        {
            if (line == null || (string.IsNullOrWhiteSpace(line.audioFileName) &&
                line.advanceMode != NarrationAdvanceMode.Operation)) return;
            if (!voice) voice = near.transform.parent.gameObject.AddComponent<NarrationController>();
            voice.StopDialogue();
            voice.lines.Clear();
            voice.lines.Add(line);
            voice.StartDialogue();
        }

        internal void Restore(bool completed)
        {
            if (voice) voice.StopDialogue();
            wasDrawing = false;
            var road = Road;
            if (completed)
            {
                var effect = (PassageGlyphEffect)stage.passage.effect;
                if (!road) PassageGlyphRoad.Restore(near, far, effect);
                else if (road.Progress < 1f) road.Advance(effect.formationSeconds);
            }
            else if (road)
            {
                near.Register(far, null);
                far.Register(near, null);
                road.gameObject.SetActive(false);
                Object.Destroy(road.gameObject);
            }
        }
    }
}
