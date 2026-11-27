using UnityEngine;

namespace NarutoVR.SealCombat.Items
{
    public sealed class TravelRationPouch : GameItemBase
    {
        [SerializeField] BoxCollider openBacking;
        public GameObject closedVisual;
        public GameObject openVisual;
        public Transform clothFlap;
        public Transform paperFlap;
        public Quaternion foldedCloth = Quaternion.identity;
        public Quaternion foldedPaper = Quaternion.identity;
        public Vector3[] packedPositions = new Vector3[4];
        public Quaternion[] packedRotations = new Quaternion[4];
        public Transform[] biscuits = new Transform[4];
        public ConsumableItem servingPrefab;

        readonly bool[] eaten = new bool[4];
        int lifted = -1;
        ConsumableItem liftedItem;
        internal int ConsumedMask
        {
            get
            {
                int mask = 0;
                for (int i = 0; i < eaten.Length; i++)
                    if (eaten[i] || (i == lifted && liftedItem && liftedItem.Remaining == 0)) mask |= 1 << i;
                return mask;
            }
        }

        internal void RestoreContents(int mask)
        {
            for (int i = 0; i < eaten.Length; i++) eaten[i] = (mask & (1 << i)) != 0;
            UpdateAppearance();
        }

        float openness;
        Quaternion clothOpen, paperOpen;
        readonly Vector3[] displayedPositions = new Vector3[4];
        readonly Quaternion[] displayedRotations = new Quaternion[4];
        internal bool HasLifted => lifted >= 0;
        public bool Open => openness >= 1f;
        public int InPouch { get; private set; } = 4;
        int ExposedBiscuit
        {
            get
            {
                for (int i = Mathf.Min(eaten.Length, biscuits.Length) - 1; i >= 0; i--)
                    if (!eaten[i] && i != lifted && biscuits[i]) return i;
                return -1;
            }
        }
        public Vector3 NextBiscuitPosition
        {
            get
            {
                int i = ExposedBiscuit;
                return i >= 0 ? BiscuitGripPoint(i, 0) : GripPosition;
            }
        }

        protected override void Awake()
        {
            base.Awake();
            if (clothFlap) clothOpen = clothFlap.localRotation;
            if (paperFlap) paperOpen = paperFlap.localRotation;
            for (int i = 0; i < biscuits.Length; i++)
            {
                if (!biscuits[i]) continue;
                displayedPositions[i] = biscuits[i].localPosition;
                displayedRotations[i] = biscuits[i].localRotation;
            }
            UpdateAppearance();
        }

        protected override void OnTaken() => SetOpenCollision(Open);

        protected override void LateUpdate()
        {
            base.LateUpdate();
            AdvanceOpening(Time.deltaTime);
        }

        internal void AdvanceOpening(float delta)
        {
            float next = Mathf.MoveTowards(openness, IsHeld ? 1f : 0f, delta / .42f);
            if (openness == next) return;
            openness = next;
            UpdateAppearance();
        }

        internal bool CanLiftBiscuit(int index) =>
            IsHeld && Open && lifted < 0 && servingPrefab && index >= 0 &&
            index == ExposedBiscuit && biscuits[index].gameObject.activeInHierarchy;

        // All three handles lie on the exposed upper edge of the outermost biscuit.
        internal Vector3 BiscuitGripPoint(int index, int side)
        {
            var piece = biscuits[index];
            var lip = new Vector3(side * .006f, .0145f, .001f);
            return piece.position + piece.parent.TransformVector(lip);
        }

        internal ConsumableItem LiftBiscuit(Transform target)
        {
            return LiftBiscuit(System.Array.IndexOf(biscuits, target));
        }

        internal ConsumableItem LiftBiscuit(Vector3 pinch, float reach)
        {
            // The standalone food lab can feed a direct near pinch without a pickup query.
            if (openBacking && transform.InverseTransformPoint(pinch).z < openBacking.center.z + openBacking.size.z * .5f)
                return null;
            int candidate = -1;
            float distance = reach * reach;
            for (int i = 0; i < eaten.Length; i++)
            {
                if (!CanLiftBiscuit(i)) continue;
                for (int side = -1; side <= 1; side++)
                {
                    float square = (pinch - BiscuitGripPoint(i, side)).sqrMagnitude;
                    if (square >= distance) continue;
                    candidate = i;
                    distance = square;
                }
            }
            return LiftBiscuit(candidate);
        }

        ConsumableItem LiftBiscuit(int candidate)
        {
            if (!CanLiftBiscuit(candidate)) return null;
            var serving = Instantiate(servingPrefab, biscuits[candidate].position, biscuits[candidate].rotation);
            if (!serving.Take())
            {
                Destroy(serving.gameObject);
                return null;
            }
            lifted = candidate;
            liftedItem = serving;
            UpdateAppearance();
            return serving;
        }

        internal void ReturnOrConsume(ConsumableItem serving)
        {
            if (lifted < 0) return;
            if (serving)
            {
                eaten[lifted] = serving.Remaining == 0;
                serving.gameObject.SetActive(false);
                Destroy(serving.gameObject);
            }
            lifted = -1;
            liftedItem = null;
            UpdateAppearance();
        }

        internal void SetOpenCollision(bool open)
        {
            if (!openBacking || !stowEnvelope || openBacking.enabled == open) return;
            openBacking.enabled = open;
            if (!Dock) stowEnvelope.enabled = !open;
        }

        void UpdateAppearance()
        {
            SetOpenCollision(IsHeld && Open);
            if (closedVisual) closedVisual.SetActive(openness == 0);
            if (openVisual) openVisual.SetActive(openness > 0);
            if (clothFlap)
                clothFlap.localRotation = Quaternion.Slerp(
                    foldedCloth, clothOpen, Mathf.SmoothStep(0, 1, openness));
            if (paperFlap)
                paperFlap.localRotation = Quaternion.Slerp(
                    foldedPaper, paperOpen,
                    Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.25f, .92f, openness)));
            int count = 0;
            for (int i = 0; i < eaten.Length; i++)
            {
                bool inPouch = i != lifted && !eaten[i];
                if (inPouch) count++;
                if (!biscuits[i]) continue;
                float appear = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.46f, .95f, openness));
                biscuits[i].SetLocalPositionAndRotation(
                    Vector3.Lerp(packedPositions[i], displayedPositions[i], appear),
                    Quaternion.Slerp(packedRotations[i], displayedRotations[i], appear));
                biscuits[i].gameObject.SetActive(inPouch && openness >= .62f);
            }
            InPouch = count;
        }
    }
}

