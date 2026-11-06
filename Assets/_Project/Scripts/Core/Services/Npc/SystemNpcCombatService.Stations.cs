using UnityEngine;

public sealed partial class SystemNpcCombatService
{
    private readonly IStationCombatService2A _stationCombat;
    private int ApplyStationWeaponDamage(string systemId, string stationId, int damage, CombatShooterType shooterType,
        string shooterId, string weaponId, string kind, string attackId, Vector3 source, Vector3 target)
    {
        if (shooterType != CombatShooterType.Npc || _stationCombat == null) return 0;
        int applied = _stationCombat.ApplyDamage(systemId, stationId, damage, shooterId, weaponId, kind, attackId);
        PublishDamagePopup(CombatTargetType.Station, stationId, applied, target, source);
        return applied;
    }
    private void ApplyStationWaveDamage(CombatWaveRuntimeState2A wave)
    {
        if (wave.StationDamaged || _stationCombat == null ||
            !_stationCombat.TryGetPosition(wave.SystemId, wave.PrimaryTargetNpcId, out var position)) return;
        position.z = 0;
        if (Vector3.Distance(wave.CenterPosition, position) > wave.CurrentRadius) return;
        wave.StationDamaged = true;
        int applied = ApplyStationWeaponDamage(wave.SystemId, wave.PrimaryTargetNpcId, wave.Damage, wave.ShooterType,
            wave.ShooterNpcId, wave.WeaponConfigId, "Wave", wave.WaveId, wave.CenterPosition, position);
        if (applied > 0) PublishWaveImpact(wave, CombatTargetType.Station, wave.PrimaryTargetNpcId, position);
    }
}
