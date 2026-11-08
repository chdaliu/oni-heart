using System.Collections.Generic;

namespace ONIHeart
{
    /// <summary>
    /// Let the heart haul in solids like a storage bin:
    /// create StorageFetch chores from the TreeFilterable checklist.
    /// Gases/liquids are not in the checklist, so they create no hauling chores and are handled by element interfaces.
    /// </summary>
    public sealed class HeartSolidCollector : KMonoBehaviour, ISim1000ms
    {
        private FilteredStorage filteredStorage;
        private bool filtersSanitized;

        protected override void OnSpawn()
        {
            base.OnSpawn();

            filteredStorage = new FilteredStorage(
                this,
                null,
                null,
                use_logic_meter: false,
                Db.Get().ChoreTypes.StorageFetch);

            // The heart has no meter animation symbol; disable the capacity meter to avoid errors.
            filteredStorage.SetHasMeter(false);
            filteredStorage.FilterChanged();
            SanitizeFilters();
        }

        public void Sim1000ms(float dt)
        {
            // TreeFilterable.OnSpawn may merge saved item tags into the filter list after our OnSpawn,
            // so sanitize once more on the first frame.
            if (!filtersSanitized)
            {
                SanitizeFilters();
            }
        }

        private void SanitizeFilters()
        {
            TreeFilterable filterable = GetComponent<TreeFilterable>();
            if (filterable == null)
            {
                return;
            }

            HashSet<Tag> accepted = new HashSet<Tag>(filterable.AcceptedTags);
            List<Tag> remove = new List<Tag>();
            foreach (Tag tag in accepted)
            {
                Element element = ElementLoader.GetElement(tag);
                if (tag == GameTags.Gas || tag == GameTags.Liquid ||
                    (element != null && !element.IsSolid))
                {
                    remove.Add(tag);
                }
            }

            if (remove.Count > 0)
            {
                for (int i = 0; i < remove.Count; i++)
                {
                    accepted.Remove(remove[i]);
                }

                filterable.UpdateFilters(accepted);
                if (filteredStorage != null)
                {
                    filteredStorage.FilterChanged();
                }
            }

            filtersSanitized = true;
        }

        protected override void OnCleanUp()
        {
            if (filteredStorage != null)
            {
                filteredStorage.CleanUp();
                filteredStorage = null;
            }

            base.OnCleanUp();
        }
    }
}
