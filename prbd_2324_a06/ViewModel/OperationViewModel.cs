﻿using NumericUpDownLib;
using prbd_2324_a06.Model;
using PRBD_Framework;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Xml;

namespace prbd_2324_a06.ViewModel
{
    public class OperationViewModel : DialogViewModelBase<Operation, PridContext>
    {
        // ajouter en parametre Tricount pour lier au reste du code
        public OperationViewModel(Operation operation) : base() {
            // initialisation des propriétés
            Tricount = Context.Tricounts.Find(operation.TricountId);
            Operation = operation;
            WindowTitle = operation.Title == null ? "Add Operation" : "Edit Operation";
            Amount = $"{Operation.Amount:F2}";
            OperationDate = Operation.Title == null ? DateTime.Today : Operation.OperationDate;
            Button = Operation.Title == null ? "Add" : "Save";
            Visible = Operation.Title == null ? Visibility.Hidden : Visibility.Visible;
            SelectedTemplate = new Template() {
                Title = "-- Choose a template --"
            };
            Participants = GetUsersTricount();
            Templates = GetTemplatesTricount();
            Templates.Add(SelectedTemplate);
            CurrentUser = App.CurrentUser;
            Initiator = operation.Title == null
                ? CurrentUser
                : Context.Users.Find(Operation.InitiatorId);
            NoTemplates = Templates.Any();
            CheckBoxItems = new ObservableCollectionFast<CheckBox>();
            Numerics = new ObservableCollectionFast<NumericUpDown>();
            TextBlocks = new ObservableCollectionFast<TextBlock>();

            // initialisation des commandes 
            SaveCommand = new RelayCommand(SaveAction, () => !HasErrors && Error == "");
            AddCommand = new RelayCommand(SaveAction,
                () => !HasErrors && Error == "");
            ApplyTemplate = new RelayCommand(ApplyAction,
                // vérification pour éviter Null Exception
                () => SelectedTemplate == null
                    ? SelectedTemplate != null
                    : SelectedTemplate.Title != "-- Choose a template --");
            SaveTemplate = new RelayCommand(SaveTemplateAction, () => !HasErrors && Error == "");
            DeleteCommand = new RelayCommand(DeleteAction);
            Console.WriteLine(SelectedTemplate.Title);
        }

        // Commandes
        public ICommand SaveCommand { get; set; }
        public ICommand AddCommand { get; set; }
        public ICommand ApplyTemplate { get; set; }
        public ICommand SaveTemplate { get; set; }
        public ICommand DeleteCommand { get; set; }

        // Attributes
        private readonly ObservableCollectionFast<CheckBox> _checkBoxItems;
        private readonly ObservableCollectionFast<NumericUpDown> _numerics;
        private readonly ObservableCollectionFast<TextBlock> _textBlocks;
        private readonly User _currentUser;
        private User _initiator;
        private Tricount _tricount;
        private readonly Operation _operation;
        private Template _selectedTemplate;
        private string _amount;
        private bool _noTemplates;
        private DateTime _operationDate;
        private string _error;
        private string _windowTitle;
        private readonly string _button;
        private Visibility _visible;
        private List<User> _participants;
        private List<Template> _templates;

        // Properties
        public ObservableCollectionFast<CheckBox> CheckBoxItems {
            get => _checkBoxItems;
            private init => SetProperty(ref _checkBoxItems, value);
        }

        public ObservableCollectionFast<NumericUpDown> Numerics {
            get => _numerics;
            private init => SetProperty(ref _numerics, value);
        }

        public ObservableCollectionFast<TextBlock> TextBlocks {
            get => _textBlocks;
            private init => SetProperty(ref _textBlocks, value);
        }

        public List<User> Participants {
            get => _participants;
            set => SetProperty(ref _participants, value);
        }
        
        public List<Template> Templates {
            get => _templates;
            set => SetProperty(ref _templates, value);
        }

        public string Error {
            get => _error;
            private set => SetProperty(ref _error, value);
        }

        public string WindowTitle {
            get => _windowTitle;
            private set => SetProperty(ref _windowTitle, value);
        }

        public bool NoTemplates {
            get => _noTemplates;
            set => SetProperty(ref _noTemplates, value);
        }

        private Operation Operation {
            get => _operation;
            init => SetProperty(ref _operation, value);
        }

        public Template SelectedTemplate {
            get => _selectedTemplate;
            set => SetProperty(ref _selectedTemplate, value);
        }

        public DateTime OperationDate {
            get => _operationDate;
            set => SetProperty(ref _operationDate, value, () => Validate());
        }

        public Tricount Tricount {
            get => _tricount;
            set => SetProperty(ref _tricount, value);
        }

        private new User CurrentUser {
            get => _currentUser;
            init => SetProperty(ref _currentUser, value);
        }

        public User Initiator {
            get => _initiator;
            set => SetProperty(ref _initiator, value);
        }

        public string Amount {
            get => _amount;
            set => SetProperty(ref _amount, value, () => {
                Validate();
                CalculAmount();
            });
        }

        public string Title {
            get => Operation?.Title;
            set => SetProperty(Operation.Title, value, Operation, (o, t) => {
                o.Title = t;
                Validate();
            });
        }

        public string Button {
            get => _button;
            private init => SetProperty(ref _button, value);
        }

        public Visibility Visible {
            get => _visible;
            set => SetProperty(ref _visible, value);
        }


        // Méthodes Commandes

        // Edit
        public override void SaveAction() {
            if (!Validate()) {
                return;
            }

            Operation.Title = Title;
            Operation.Amount = double.Parse(Amount);
            Operation.OperationDate = OperationDate;
            Operation.InitiatorId = Initiator.UserId;
            if (!Tricount.GetOperations().ToList().Contains(Operation)) {
                Context.Add(Operation);
            }

            SaveWeights();
            Context.SaveChanges();
            RaisePropertyChanged();
            NotifyColleagues(App.Messages.MSG_OPERATION_CHANGED, Operation);
            NotifyColleagues(App.Messages.MSG_OPERATION_TRICOUNT_CHANGED, Tricount);
            Close();
        }

        // Save les poids de l'opération
        private void SaveWeights() {
            if (Numerics == null) {
                return;
            }

            foreach (var item in Numerics) {
                User user = User.GetUserByName(item.Name);
                // Si paire operation-user existante parmi les répartitions -> modifications des poids
                if (Operation.Repartitions.Any(r => r.UserId == user.UserId && r.OperationId == Operation.Id)) {
                    UpdateWeight(user.UserId, item.Value);
                    Console.WriteLine("yo");
                } else if (item.Value > 0) {
                    // Nouvelle répartition si paire operation-user inexistante parmi les répartitions
                    Operation.Repartitions.Add(new Repartition(Operation.Id, user.UserId, item.Value));
                    Console.WriteLine("ya");
                }
            }
        }

        // Modifie poid associé à un user dans une répétition
        private void UpdateWeight(int userId, int newWeight) {
            // Rechercher la répartition correspondante pour l'utilisateur spécifié
            Repartition repartition =
                Operation.Repartitions.FirstOrDefault(r => r.UserId == userId && r.OperationId == Operation.Id);
            if (repartition == null) {
                return;
            }
            // Mettre à jour le poids de l'utilisateur
            repartition.Weight = newWeight;
            Context.SaveChanges();
        }


        // Delete 
        private void DeleteAction() {
            Operation.Delete();
            NotifyColleagues(App.Messages.MSG_OPERATION_CHANGED, Operation);
            NotifyColleagues(App.Messages.MSG_OPERATION_TRICOUNT_CHANGED, Tricount);
            NotifyColleagues(App.Messages.MSG_DELETED);
            Close();
        }

        private void SaveTemplateAction() {
        }

        // Applique la template selectionnée 
        private void ApplyAction() {
            // vérifications 
            if (SelectedTemplate != null && Numerics != null) {
                string title = SelectedTemplate.Title;
                if (Tricount.GetTemplateByTitle(title) != null) {
                    // récupération template sélectionné
                    var t = Tricount.GetTemplateByTitle(title);
                    // association user-weight dans un dictionnaire
                    Dictionary<string, int> UserWeight = t.GetUsersAndWeightsByTricountId();
                    // attribution des valeurs sinon 0 si user inexistant dans la template
                    foreach (var item in Numerics) {
                        if (UserWeight.ContainsKey(item.Name))
                            item.Value = UserWeight[item.Name];
                        else
                            item.Value = 0;
                    }
                }
            }
        }

        // Méthodes de validation
        public override bool Validate() {
            ClearErrors();
            Operation.Validate();
            IsValidAmount();
            IsValidDate();
            AddErrors(Operation.Errors);
            Error = !ValidateCheckBoxes() ? "You must check at least one participant !" : "";

            return !HasErrors;
        }

        // Check if at least one checkbox is checked
        private bool ValidateCheckBoxes() {
            return CheckBoxItems == null || CheckBoxItems.Any(item => item.IsChecked != null && (bool)item.IsChecked);
        }

        // Return Users of the Operation's Tricount.
        protected internal List<User> GetUsersTricount() {
            Tricount tricount = Tricount;
            List<User> participants = new List<User>();
            foreach (User user in tricount.GetParticipants()) {
                participants.Add(user);
            }

            return participants;
        }

        // Return Templates of the Operation's Tricount.
        protected internal List<Template> GetTemplatesTricount() {
            Tricount tricount = Tricount;
            List<Template> templates = new List<Template>();
            foreach (Template template in tricount.GetTemplates()) {
                templates.Add(template);
            }

            return templates;
        }

        protected internal List<Repartition> GetRepartitions() {
            return Operation.Repartitions.ToList();
        }

        // Validation méthode for string amount -> we need a string because of textbox
        private void IsValidAmount() {
            if (Amount is { Length: > 0 }) {
                if (!double.TryParse(Amount, out double value))
                    AddError(nameof(Amount), "Not an Integer");
                if (double.Parse(Amount) < 0.01) {
                    AddError(nameof(Amount), "minimum 1 cent");
                }
            } else
                AddError(nameof(Amount), "Can't be empty !");
        }

        private void IsValidDate() {
            if (OperationDate < Tricount.CreatedAt) {
                AddError(nameof(OperationDate), $"Cannot be before {Tricount.CreatedAt:dd-MM-yyyy}.");
            }

            if (OperationDate > DateTime.Today) {
                AddError(nameof(OperationDate), "cannot be in the future.");
            }
        }

        public void CalculAmount() {
            if (Amount is { Length: > 0 }) {
                int totalWeight = 0;
                if (Numerics != null && TextBlocks != null) {
                    int[] weights = new int[Numerics.Count];

                    // Calcul du total des poids
                    var i = 0;
                    foreach (var item in Numerics) {
                        totalWeight += item.Value;
                        weights[i] = item.Value;
                        i++;
                    }

                    // insertion montants dans textblock
                    i = 0;
                    double part = totalWeight < 1
                        ? double.Parse(Amount) * totalWeight
                        : double.Parse(Amount) / totalWeight;
                    foreach (var item in TextBlocks) {
                        item.Text = $"{part * weights[i]:F2} €";
                        i++;
                    }
                }
            } else {
                AddError(nameof(Amount), "Can't be empty !");
            }
        }

        protected internal void Close() {
            NotifyColleagues(App.Messages.MSG_CLOSE_OPERATION_WINDOW, Operation);
        }
    }
}