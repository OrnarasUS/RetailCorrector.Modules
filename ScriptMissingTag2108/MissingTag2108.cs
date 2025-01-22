// Подробнее об ошибке 420-2108:
// https://clck.ru/3Fsyqu

using RetailCorrector.API.Data;
using RetailCorrector.API.Types;
using RetailCorrector.API;
using System.ComponentModel;

namespace ScriptMissingTag2108
{
    [DisplayName("Ошибка 420 (Отсутствует тег 2108)")]
    public class MissingTag2108 : IScript
    {
        public bool NeedCancel => false;
        public bool Filter(Receipt receipt) =>
            receipt.Items.Any(i => i.MeasureUnit.Name == "-");

        private readonly Dictionary<string, MeasureUnit> Items = [];

        public Task<List<Receipt>> Edit(List<Receipt> origin)
        {
            foreach (var receipt in origin)
            {
                foreach (var item in receipt.Items)
                {
                    if (!Items.ContainsKey(item.Name))
                        Items.Add(item.Name, item.MeasureUnit);
                }
            }

            // Setter MeasureUnits


            for (var i = 0; i < origin.Count; i++)
            {
                var r = origin[i];
                for (var j = 0; j < r.Items.Length; j++)
                {
                    var p = r.Items[j];
                    p.MeasureUnit = Items[p.Name];
                    r.Items[j] = p;
                }
                origin[i] = r;
            }

            return Task.FromResult(origin);
        }
    }
}
