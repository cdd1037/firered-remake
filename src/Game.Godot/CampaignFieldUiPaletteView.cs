using Godot;

public partial class CampaignView
{
    private SourceGameFont? _fieldUiFont;
    private int _fieldUiCoefficient=-1;
    private ushort _fieldUiTarget;
    private SourceGameFont FieldPaletteUiFont(int coefficient,ushort target=0)
    {
        if(coefficient==0)return _sourceNormal;
        if(_fieldUiCoefficient!=coefficient||_fieldUiTarget!=target)
        {
            _fieldUiFont?.Dispose();
            _fieldUiFont=_sourceNormal.CreatePaletteVariant(coefficient,target);
            _fieldUiCoefficient=coefficient;_fieldUiTarget=target;
        }
        return _fieldUiFont!;
    }
    private void ClearFieldUiPalette()
    {
        _fieldUiFont?.Dispose();_fieldUiFont=null;_fieldUiCoefficient=-1;
    }

}
