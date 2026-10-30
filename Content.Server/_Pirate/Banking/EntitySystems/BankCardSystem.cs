using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server.Access.Systems;
using Content.Shared._Pirate.Banking.Components;
using Content.Server.Cargo.Systems;
using Content.Server.CartridgeLoader;
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

public sealed partial class BankCardSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IPrototypeManager _protoMan = default!;
    [Dependency] private readonly IdCardSystem _idCardSystem = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly CargoSystem _cargo = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly InventorySystem _inventorySystem = default!;
    [Dependency] private readonly BankCartridgeSystem _bankCartridge = default!;
    [Dependency] private readonly JobSystem _job = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly CartridgeLoaderSystem _cartridgeLoader = default!;

    private const int SalaryDelay = 1200; // Preserve the 30-minute salary rate.

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

        if (_salaryTimer < SalaryDelay)
            return;

        _salaryTimer %= SalaryDelay;
        PaySalary();
    }

    private void PaySalary()
    {
        var paidAccounts = new HashSet<int>();
        foreach (var account in _accounts.Where(account =>
                     account.Mind != null &&
                     TryComp(GetEntity(account.Mind.Value), out MindComponent? mindComp) &&
                     mindComp.UserId != null &&
                     mindComp.CurrentEntity != null &&
                     _playerManager.TryGetSessionById(mindComp.UserId.Value, out _) &&
                     !_mobState.IsDead(mindComp.CurrentEntity.Value)))
        {
            if (account.Mind == null) continue;
            var salary = (int) Math.Round(GetSalary(GetEntity(account.Mind.Value)) * 2d / 3d);
            if (salary <= 0 || !TryChangeBalance(account.AccountId, salary, BankTransactionType.Salary))
                continue;

            paidAccounts.Add(account.AccountId);
        }

        NotifySalaryPayments(paidAccounts);
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
        _salaryTimer = 0f;
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

            TryChangeBalance(bankAccount.AccountId, GetSalary(mind.Mind) + 100 - bankAccount.Balance,
                BankTransactionType.OpeningBalance);
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

    /// <summary>
    /// Ensures the mob's ID card has a bank account and sets it to a flat balance, with no salary attached.
    /// Used for roles (e.g. nuke operatives) that don't go through the normal job-spawn pipeline.
    /// </summary>
    public bool TryGiveFlatBalance(EntityUid mob, int balance)
    {
        if (!_idCardSystem.TryFindIdCard(mob, out var id))
            return false;

        var cardEntity = id.Owner;
        var bankCardComponent = EnsureComp<BankCardComponent>(cardEntity);

        if (!bankCardComponent.AccountId.HasValue || !TryGetAccount(bankCardComponent.AccountId.Value, out var bankAccount))
            return false;

        if (!TryChangeBalance(bankAccount.AccountId, balance - bankAccount.Balance, BankTransactionType.OpeningBalance))
            return false;
        bankAccount.Name = Name(mob);

        if (TryComp<MindContainerComponent>(mob, out var mindContainer) &&
            mindContainer.Mind.HasValue &&
            TryComp(mindContainer.Mind.Value, out MindComponent? mindComponent))
        {
            mindComponent.AddMemory(new Memory("PIN", bankAccount.AccountPin.ToString()));
            mindComponent.AddMemory(new Memory(Loc.GetString("character-info-memories-account-number"),
                bankAccount.AccountId.ToString()));
            bankAccount.Mind = GetNetEntity(mindContainer.Mind.Value);
        }

        if (_inventorySystem.TryGetSlotEntity(mob, "id", out var pdaUid))
        {
            BankCartridgeComponent? comp = null;
            var programs = _cartridgeLoader.GetInstalled(pdaUid.Value);
            var program = programs.ToList().Find(p => TryComp(p, out comp));
            if (comp != null)
            {
                bankAccount.CartridgeUid = GetNetEntity(program);
                comp.AccountId = bankAccount.AccountId;
            }
        }

        return true;
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

    public bool TryChangeBalance(int accountId, int amount,
        BankTransactionType type = BankTransactionType.Adjustment, string details = "")
    {
        if (!TryGetAccount(accountId, out var account))
            return false;

        var newBalance = (long) account.Balance + amount;
        if (newBalance < 0 || newBalance > int.MaxValue)
            return false;

        if (amount < 0 && account.Arrested && IsSpending(type))
            return false;

        if (amount == 0)
            return true;

        account.Balance = (int) newBalance;
        RecordTransaction(account, amount, type, details);
        if (account.CartridgeUid != null)
            _bankCartridge.UpdateUiState(GetEntity(account.CartridgeUid.Value));

        return true;
    }
}
