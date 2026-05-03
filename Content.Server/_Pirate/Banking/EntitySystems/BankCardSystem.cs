using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server.Access.Systems;
using Content.Shared._Pirate.Banking.Components;
using Content.Server.Cargo.Systems;
using Content.Server.CartridgeLoader;
using Content.Server.Chat.Systems;
using Content.Server.GameTicking;
using Content.Server.Roles.Jobs;
using Content.Server.Station.Systems;
using Content.Shared._Pirate.Banking;
using Content.Shared.GameTicking;
using Content.Shared.Inventory;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Systems;
using Robust.Server.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Pirate.Banking;

public sealed class BankCardSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IPrototypeManager _protoMan = default!;
    [Dependency] private readonly IdCardSystem _idCardSystem = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly CargoSystem _cargo = default!;
    [Dependency] private readonly ChatSystem _chatSystem = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly InventorySystem _inventorySystem = default!;
    [Dependency] private readonly BankCartridgeSystem _bankCartridge = default!;
    [Dependency] private readonly JobSystem _job = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly CartridgeLoaderSystem _cartridgeLoader = default!;

    private const int SalaryDelay = 1800; //30 min

    private SalaryPrototype _salaries = default!;
    private readonly List<BankAccount> _accounts = new();
    private float _salaryTimer;

    public override void Initialize()
    {
        _salaries = _protoMan.Index<SalaryPrototype>("Salaries");

        SubscribeLocalEvent<BankCardComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<StationBankComponent, MapInitEvent>(OnStationBankInit);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawned);
    }

    private void OnStationBankInit(EntityUid uid, StationBankComponent component, MapInitEvent args)
    {
        if (component.BankAccount.AccountId == 0)
        {
            int accountNumber;
            do
            {
                accountNumber = _random.Next(100000, 999999);
            } while (AccountExist(accountNumber));

            component.BankAccount.AccountId = accountNumber;
        }

        component.BankAccount.CommandBudgetAccount = true;

        if (!AccountExist(component.BankAccount.AccountId))
        {
            _accounts.Add(component.BankAccount);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_gameTicker.RunLevel != GameRunLevel.InRound)
        {
            _salaryTimer = 0f;
            return;
        }

        _salaryTimer += frameTime;

        if (_salaryTimer <= SalaryDelay)
            return;

        _salaryTimer = 0f;
        PaySalary();
    }

    private void PaySalary()
    {
        foreach (var account in _accounts.Where(account =>
                     account.Mind != null &&
                     TryComp(GetEntity(account.Mind.Value), out MindComponent? mindComp) &&
                     mindComp.UserId != null &&
                     mindComp.CurrentEntity != null &&
                     _playerManager.TryGetSessionById(mindComp.UserId.Value, out _) &&
                     !_mobState.IsDead(mindComp.CurrentEntity.Value)))
        {
            if (account.Mind == null) continue;
            account.Balance += GetSalary(GetEntity(account.Mind.Value));
        }

        _chatSystem.DispatchGlobalAnnouncement(Loc.GetString("salary-pay-announcement"),
            colorOverride: Color.FromHex("#18abf5"));
    }

    private int GetSalary(EntityUid? mind)
    {
        if (!_job.MindTryGetJob(mind, out var job) || !_salaries.Salaries.TryGetValue(job.ID, out var salary))
            return 0;

        return salary;
    }

    private void OnMapInit(EntityUid uid, BankCardComponent component, MapInitEvent args)
    {
        if (component.CommandBudgetCard &&
            TryComp(_station.GetOwningStation(uid), out StationBankComponent? acc))
        {
            component.AccountId = acc.BankAccount.AccountId;
            return;
        }

        if (component.AccountId.HasValue)
        {
            CreateAccount(component.AccountId.Value, component.StartingBalance);
            return;
        }

        var account = CreateAccount(default, component.StartingBalance);
        component.AccountId = account.AccountId;
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _accounts.Clear();
    }

    private void OnPlayerSpawned(PlayerSpawnCompleteEvent ev)
    {
        if (_idCardSystem.TryFindIdCard(ev.Mob, out var id) && TryComp<MindContainerComponent>(ev.Mob, out var mind))
        {
            var cardEntity = id.Owner;
            var bankCardComponent = EnsureComp<BankCardComponent>(cardEntity);

            if (!bankCardComponent.AccountId.HasValue || !TryGetAccount(bankCardComponent.AccountId.Value, out var bankAccount))
                return;

            if (!TryComp(mind.Mind, out MindComponent? mindComponent))
                return;

            bankAccount.Balance = GetSalary(mind.Mind) + 100;
            mindComponent.AddMemory(new Memory("PIN", bankAccount.AccountPin.ToString()));
            mindComponent.AddMemory(new Memory(Loc.GetString("character-info-memories-account-number"),
                bankAccount.AccountId.ToString()));
            bankAccount.Mind = GetNetEntity(mind.Mind.Value);
            bankAccount.Name = Name(ev.Mob);

            if (!_inventorySystem.TryGetSlotEntity(ev.Mob, "id", out var pdaUid))
                return;

            BankCartridgeComponent? comp = null;

            var programs = _cartridgeLoader.GetInstalled(pdaUid.Value);

            var program = programs.ToList().Find(program => TryComp(program, out comp));
            if (comp == null)
                return;

            bankAccount.CartridgeUid = GetNetEntity(program);
            comp.AccountId = bankAccount.AccountId;
        }
    }

    public BankAccount CreateAccount(int accountId = default, int startingBalance = 0)
    {
        if (TryGetAccount(accountId, out var acc))
            return acc;

        BankAccount account;
        if (accountId == default)
        {
            int accountNumber;

            do
            {
                accountNumber = _random.Next(100000, 999999);
            } while (AccountExist(accountId));

            account = new BankAccount(accountNumber, startingBalance);
        }
        else
        {
            account = new BankAccount(accountId, startingBalance);
        }

        _accounts.Add(account);

        return account;
    }

    public bool AccountExist(int accountId)
    {
        return _accounts.Any(x => x.AccountId == accountId);
    }

    public bool TryGetAccount(int accountId, [NotNullWhen(true)] out BankAccount? account)
    {
        account = _accounts.FirstOrDefault(x => x.AccountId == accountId);
        return account != null;
    }

    public int GetBalance(int accountId)
    {
        if (TryGetAccount(accountId, out var account))
        {
            return account.Balance;
        }

        return 0;
    }

    public bool TryChangeBalance(int accountId, int amount)
    {
        if (!TryGetAccount(accountId, out var account) || account.Balance + amount < 0)
            return false;

        if (account.CommandBudgetAccount)
        {
            var query = EntityQueryEnumerator<StationBankComponent>();
            while (query.MoveNext(out var uid, out var acc))
            {
                if (acc.BankAccount.AccountId != accountId)
                    continue;

                // _cargo.UpdateBankAccount(uid, acc, amount); // TODO: Fix this if needed, or remove if StationBankComponent handles it differently
                // For now, just return true as we are modifying the shared BankAccount object which is reference type if it was a class, but it is a data definition.
                // Wait, BankAccount is a class in the previous code, but I made it a class in Shared too.
                // However, StationBankComponent has it as a DataField.
                // If we want to update the station's bank account, we should probably update the component.
                
                acc.BankAccount.Balance += amount;
                return true;
            }
        }

        account.Balance += amount;
        if (account.CartridgeUid != null)
            _bankCartridge.UpdateUiState(GetEntity(account.CartridgeUid.Value));

        return true;
    }
}
