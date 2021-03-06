﻿using SatisfactorySaveParser.PropertyTypes;

namespace SatisfactorySaveEditor.ViewModel.Property
{
    public class BytePropertyViewModel : SerializedPropertyViewModel
    {
        private readonly ByteProperty model;

        private string value;
        private string type;

        public string Value
        {
            get => value;
            set { Set(() => Value, ref this.value, value); }
        }

        public string Type
        {
            get => type;
            set { Set(() => Type, ref type, value); }
        }

        public BytePropertyViewModel(ByteProperty byteProperty) : base(byteProperty)
        {
            model = byteProperty;

            value = model.Value;
            type = model.Type;
        }

        public override void ApplyChanges()
        {
            model.Value = value;
            model.Type = type;
        }
    }
}
