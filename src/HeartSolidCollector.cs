using System.Collections.Generic;

namespace ONIHeart
{
    /// <summary>
    /// 让元素之心像容器箱子一样把固体搬进来：
    /// 根据 TreeFilterable 的勾选列表创建 StorageFetch 搬运任务。
    /// 气体/液体不在勾选列表中，因此不会产生搬运任务，仍由元素接口处理。
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

            // 元素之心没有 meter 动画符号，关闭容量表盘以免报错。
            filteredStorage.SetHasMeter(false);
            filteredStorage.FilterChanged();
            SanitizeFilters();
        }

        public void Sim1000ms(float dt)
        {
            // TreeFilterable.OnSpawn 可能在我们的 OnSpawn 之后才把存档里的
            // 物品标签合并进过滤列表，所以第一帧再清理一次。
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
