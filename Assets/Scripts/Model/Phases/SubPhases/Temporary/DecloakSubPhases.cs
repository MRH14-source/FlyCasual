﻿using GameModes;
using Obstacles;
using RuleSets;
using Ship;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SubPhases
{

    public class DecloakDecisionSubPhase : DecisionSubPhase
    {

        public override void PrepareDecision(System.Action callBack)
        {
            InfoText = "Perform decloak?";

            DecisionOwner = Selection.ThisShip.Owner;

            AddDecision("Yes", Decloak);
            AddDecision("No", SkipDecloak);

            AddTooltip("Yes", "https://raw.githubusercontent.com/guidokessels/xwing-data/master/images/reference-cards/Decloak.png");

            DefaultDecisionName = "No";

            callBack();
        }

        private void Decloak(object sender, System.EventArgs e)
        {
            Phases.CurrentSubPhase.Pause();
            UI.CallHideTooltip();

            Phases.StartTemporarySubPhaseOld(
                "Decloak",
                typeof(DecloakPlanningSubPhase),
                Phases.CurrentSubPhase.CallBack
            );
        }

        private void SkipDecloak(object sender, System.EventArgs e)
        {
            UI.CallHideTooltip();
            CallBack();
        }

    }

}

namespace SubPhases
{

    public class DecloakPlanningSubPhase : GenericSubPhase
    {
        private int updatesCount = 0;

        public GameObject ShipStand;
        private ObstaclesStayDetectorForced obstaclesStayDetectorBase;
        private ObstaclesStayDetectorForced obstaclesStayDetectorMovementTemplate;

        Dictionary<string, Vector3> AvailableDecloakDirections = new Dictionary<string, Vector3>();
        public string SelectedDecloakHelper;

        public float helperDirection;
        public bool inReposition;

        List<Actions.DecloakTemplates> availableTemplates = new List<Actions.DecloakTemplates>();
        Actions.DecloakTemplateVariants selectedTemplateVariant;
        public GameObject DecloakTemplate;
        public float HelperDirection;
        public GameObject TemporaryShipBase;

        public override void Start()
        {
            Name = "Decloak planning";
            IsTemporary = true;
            UpdateHelpInfo();

            StartDecloakPlanning();
        }

        public void StartDecloakPlanning()
        {
            GenerateListOfAvailableTemplates();
            AskToSelectTemplate(PerfromTemplatePlanning);
        }

        public void PerfromTemplatePlanning()
        {
            RuleSet.Instance.DecloakTemplatePlanning();
        }

        public void PerfromTemplatePlanningFirstEdition()
        {
        }

        public void PerfromTemplatePlanningSecondEdition()
        {
            if (IsBoostTemplate(selectedTemplateVariant))
            {
                ShowBarrelRollTemplate();
                ShowTemporaryShipBase();
                ConfirmPosition();
            }
            else
            {
                Triggers.RegisterTrigger(new Trigger()
                {
                    Name = "Decloak position",
                    TriggerType = TriggerTypes.OnAbilityDirect,
                    TriggerOwner = Selection.ThisShip.Owner.PlayerNo,
                    EventHandler = AskDecloakPosition
                });

                Triggers.ResolveTriggers(TriggerTypes.OnAbilityDirect, ConfirmPosition);
            }
        }

        private bool IsBoostTemplate(Actions.DecloakTemplateVariants selectedTemplateVariant)
        {
            return selectedTemplateVariant == Actions.DecloakTemplateVariants.Straight2Forward
                || selectedTemplateVariant == Actions.DecloakTemplateVariants.Bank2ForwardLeft
                || selectedTemplateVariant == Actions.DecloakTemplateVariants.Bank2ForwardRight;
        }

        private void ConfirmPosition()
        {
            if (TemporaryShipBase == null)
            {
                PerfromTemporaryShipBasePlanning();
            }
            else
            {
                GameMode.CurrentGameMode.TryConfirmDecloakPosition(TemporaryShipBase.transform.position, selectedTemplateVariant.ToString(), DecloakTemplate.transform.position, DecloakTemplate.transform.eulerAngles);
            }
        }

        private void PerfromTemporaryShipBasePlanning()
        {
            ShowTemporaryShipBase();

            StartReposition();
        }

        private void StartReposition()
        {
            //if (!useMobileControls)
            //{

            Roster.SetRaycastTargets(false);
            if (Selection.ThisShip.Owner.GetType() == typeof(Players.HumanPlayer))
            {
                inReposition = true;
            }

            //}
        }

        private void AskDecloakPosition(object sender, System.EventArgs e)
        {
            // TODO: skip for boost

            DecloakPositionDecisionSubPhase selectBarrelRollPosition = (DecloakPositionDecisionSubPhase)Phases.StartTemporarySubPhaseNew(
                 Name,
                 typeof(DecloakPositionDecisionSubPhase),
                 Triggers.FinishTrigger
            );

            selectBarrelRollPosition.AddDecision("Forward", delegate { SetBarrelRollPosition(0.5f); }, isCentered: true);
            selectBarrelRollPosition.AddDecision("Center", delegate { SetBarrelRollPosition(0); }, isCentered: true);
            selectBarrelRollPosition.AddDecision("Backwards", delegate { SetBarrelRollPosition(-0.5f); }, isCentered: true);

            selectBarrelRollPosition.InfoText = "Decloak: Select position";

            selectBarrelRollPosition.DefaultDecisionName = "Center";

            selectBarrelRollPosition.RequiredPlayer = Selection.ThisShip.Owner.PlayerNo;

            selectBarrelRollPosition.Start();
        }

        private void SetBarrelRollPosition(float position)
        {
            ShowBarrelRollTemplate();
            ShowTemporaryShipBase();

            //ProcessTemplatePositionSlider(-0.5f);
            ProcessTemporaryShipBaseSlider(position);

            DecisionSubPhase.ConfirmDecision();
        }

        private void ShowBarrelRollTemplate()
        {
            GameObject template = GetCurrentDecloakHelperTemplateGO();
            if (Selection.ThisShip.Owner.GetType() != typeof(Players.NetworkOpponentPlayer)) template.SetActive(true);
            HelperDirection = GetDirectionModifier(selectedTemplateVariant);
            obstaclesStayDetectorMovementTemplate = template.GetComponentInChildren<ObstaclesStayDetectorForced>();
            obstaclesStayDetectorMovementTemplate.TheShip = Selection.ThisShip;
        }

        private void ShowTemporaryShipBase()
        {
            if (TemporaryShipBase == null)
            {
                GameObject prefab = (GameObject)Resources.Load(TheShip.ShipBase.TemporaryPrefabPath, typeof(GameObject));
                TemporaryShipBase = MonoBehaviour.Instantiate(prefab, this.GetCurrentDecloakHelperTemplateFinisherBasePositionGO().transform.position, this.GetCurrentDecloakHelperTemplateFinisherBasePositionGO().transform.rotation, BoardTools.Board.GetBoard());
                TemporaryShipBase.transform.Find("ShipBase").Find("ShipStandInsert").Find("ShipStandInsertImage").Find("default").GetComponent<Renderer>().material = TheShip.Model.transform.Find("RotationHelper").Find("RotationHelper2").Find("ShipAllParts").Find("ShipBase").Find("ShipStandInsert").Find("ShipStandInsertImage").Find("default").GetComponent<Renderer>().material;
                TemporaryShipBase.transform.Find("ShipBase").Find("ObstaclesStayDetector").gameObject.AddComponent<ObstaclesStayDetectorForced>();
                obstaclesStayDetectorBase = TemporaryShipBase.GetComponentInChildren<ObstaclesStayDetectorForced>();
                obstaclesStayDetectorBase.TheShip = TheShip;

                // TODO: Mobile controls
                // if (useMobileControls) ProcessTemporaryShipBaseSlider(SliderMenu.GetSliderValue());
            }
        }

        public void ProcessTemplatePositionSlider(float value)
        {
            Vector3 newPositionRel = Vector3.zero;

            newPositionRel.x = HelperDirection * TheShip.ShipBase.HALF_OF_SHIPSTAND_SIZE;
            newPositionRel.z = value;

            Vector3 newPositionAbs = TheShip.TransformPoint(newPositionRel);

            DecloakTemplate.transform.position = newPositionAbs;
        }

        public void ProcessTemporaryShipBaseSlider(float value)
        {
            GameObject finisherBase = GetCurrentDecloakHelperTemplateFinisherBasePositionGO();
            Vector3 positionAbs = finisherBase.transform.TransformPoint(new Vector3(0, 0, value*1.18f));
            TemporaryShipBase.transform.position = positionAbs;
        }

        private GameObject GetCurrentDecloakHelperTemplateFinisherBasePositionGO()
        {
            return GetCurrentDecloakHelperTemplateFinisherGO().transform.Find("BasePosition").gameObject;
        }

        private GameObject GetCurrentDecloakHelperTemplateFinisherGO()
        {
            return GetCurrentDecloakHelperTemplateGO().transform.Find("Finisher").gameObject;
        }

        private float GetDirectionModifier(Actions.DecloakTemplateVariants templateVariant)
        {
            return (templateVariant.ToString().Contains("Left")) ? -1 : 1;
        }

        protected void GenerateListOfAvailableTemplates()
        {
            availableTemplates = Selection.ThisShip.GetAvailableDecloakTemplates();
        }

        private void AskToSelectTemplate(Action callback)
        {
            if (availableTemplates.Count > 0)
            {
                RegisterDirectionDecisionTrigger(callback);
            }
            else
            {
                Console.Write("No available templates for Decloak!", LogTypes.Errors, true, "red");
            }
        }

        private void RegisterDirectionDecisionTrigger(Action callback)
        {
            Triggers.RegisterTrigger(new Trigger()
            {
                Name = "Select direction and template",
                TriggerType = TriggerTypes.OnAbilityDirect,
                TriggerOwner = Selection.ThisShip.Owner.PlayerNo,
                EventHandler = StartSelectTemplateSubphase
            });

            Triggers.ResolveTriggers(TriggerTypes.OnAbilityDirect, callback);
        }

        protected void StartSelectTemplateSubphase(object sender, System.EventArgs e)
        {
            DecloakDirectionDecisionSubPhase selectDecloakTemplate = (DecloakDirectionDecisionSubPhase)Phases.StartTemporarySubPhaseNew(
                Name,
                typeof(DecloakDirectionDecisionSubPhase),
                Triggers.FinishTrigger
            );

            foreach (var template in availableTemplates)
            {
                switch (template)
                {
                    case Actions.DecloakTemplates.Straight2:
                        selectDecloakTemplate.AddDecision("Forward Straight 2", delegate { SelectTemplate(Actions.DecloakTemplateVariants.Straight2Forward); DecisionSubPhase.ConfirmDecision();}, isCentered: true);
                        selectDecloakTemplate.AddDecision("Left Straight 2", delegate { SelectTemplate(Actions.DecloakTemplateVariants.Straight2Left); DecisionSubPhase.ConfirmDecision(); });
                        selectDecloakTemplate.AddDecision("Right Straight 2", delegate { SelectTemplate(Actions.DecloakTemplateVariants.Straight2Right); DecisionSubPhase.ConfirmDecision(); });
                        break;
                    case Actions.DecloakTemplates.Bank2:
                        selectDecloakTemplate.AddDecision("Forward Bank 2 Left", delegate { SelectTemplate(Actions.DecloakTemplateVariants.Bank2ForwardLeft); DecisionSubPhase.ConfirmDecision(); });
                        selectDecloakTemplate.AddDecision("Forward Bank 2 Right", delegate { SelectTemplate(Actions.DecloakTemplateVariants.Bank2ForwardRight); DecisionSubPhase.ConfirmDecision(); });
                        selectDecloakTemplate.AddDecision("Left Bank 2 Forward", delegate { SelectTemplate(Actions.DecloakTemplateVariants.Bank2LeftForward); DecisionSubPhase.ConfirmDecision(); });
                        selectDecloakTemplate.AddDecision("Right Bank 2 Forward", delegate { SelectTemplate(Actions.DecloakTemplateVariants.Bank2RightForward); DecisionSubPhase.ConfirmDecision(); });
                        selectDecloakTemplate.AddDecision("Left Bank 2 Backwards", delegate { SelectTemplate(Actions.DecloakTemplateVariants.Bank2LeftBackwards); DecisionSubPhase.ConfirmDecision(); });
                        selectDecloakTemplate.AddDecision("Right Bank 2 Backwards", delegate { SelectTemplate(Actions.DecloakTemplateVariants.Bank2RightBackwards); DecisionSubPhase.ConfirmDecision(); });
                        break;
                    default:
                        break;
                }
            }

            selectDecloakTemplate.InfoText = "Decloak: Select template and direction";

            selectDecloakTemplate.DefaultDecisionName = selectDecloakTemplate.GetDecisions().First().Name;

            selectDecloakTemplate.RequiredPlayer = Selection.ThisShip.Owner.PlayerNo;

            selectDecloakTemplate.Start();
        }

        public void SelectTemplate(Actions.DecloakTemplateVariants templateVariant)
        {
            selectedTemplateVariant = templateVariant;
            DecloakTemplate = GetCurrentDecloakHelperTemplateGO();
        }

        private GameObject GetCurrentDecloakHelperTemplateGO()
        {
            return Selection.ThisShip.GetDecloakHelper().Find(selectedTemplateVariant.ToString()).gameObject;
        }

        // NOT USED
        private void PrepareDragging()
        {
            GameObject prefab = (GameObject)Resources.Load(Selection.ThisShip.ShipBase.TemporaryPrefabPath, typeof(GameObject));
            ShipStand = MonoBehaviour.Instantiate(prefab, Selection.ThisShip.GetPosition(), Selection.ThisShip.GetRotation(), BoardTools.Board.GetBoard());
            ShipStand.transform.Find("ShipBase").Find("ShipStandInsert").Find("ShipStandInsertImage").Find("default").GetComponent<Renderer>().material = Selection.ThisShip.Model.transform.Find("RotationHelper").Find("RotationHelper2").Find("ShipAllParts").Find("ShipBase").Find("ShipStandInsert").Find("ShipStandInsertImage").Find("default").GetComponent<Renderer>().material;
            ShipStand.transform.Find("ShipBase").Find("ObstaclesStayDetector").gameObject.AddComponent<ObstaclesStayDetectorForced>();
            obstaclesStayDetectorBase = ShipStand.GetComponentInChildren<ObstaclesStayDetectorForced>();

            MovementTemplates.CurrentTemplate = MovementTemplates.GetMovement2Ruler();
            MovementTemplates.CurrentTemplate.position = Selection.ThisShip.TransformPoint(new Vector3(0.5f, 0, -0.25f));

            foreach (Transform decloakHelper in Selection.ThisShip.GetDecloakHelper())
            {
                AvailableDecloakDirections.Add(decloakHelper.name, decloakHelper.Find("Finisher").position);
            }

            Roster.SetRaycastTargets(false);
            TurnOnDragging();
        }

        private void TurnOnDragging()
        {
            if (Selection.ThisShip.Owner.GetType() == typeof(Players.HumanPlayer)) inReposition = true;
        }

        public override void Update()
        {
            if (inReposition)
            {
                SelectDecloakHelper();
            }
        }

        public override void Pause()
        {
            inReposition = false;
        }

        public override void Resume()
        {
            TurnOnDragging();
        }

        private void SelectDecloakHelper()
        {
            RaycastHit hit;
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(ray, out hit))
            {
                ShowNearestDecloakHelper(GetNearestDecloakHelper(new Vector3(hit.point.x, 0f, hit.point.z)));
            }
        }

        private void ShowNearestDecloakHelper(string name)
        {
            if (SelectedDecloakHelper != name)
            {
                if (name == "Forward")
                {
                    MovementTemplates.CurrentTemplate.gameObject.SetActive(false);

                    GetDecloakHelperByName(name).gameObject.SetActive(true);

                    Transform newBase = Selection.ThisShip.GetDecloakHelper().Find(name + "/Finisher/BasePosition");
                    ShipStand.transform.position = newBase.position;
                    ShipStand.transform.rotation = newBase.rotation;
                }
                else
                {
                    if (!string.IsNullOrEmpty(SelectedDecloakHelper))
                    {
                        GetDecloakHelperByName(SelectedDecloakHelper).gameObject.SetActive(false);
                    }

                    MovementTemplates.CurrentTemplate.gameObject.SetActive(true);

                    PerfromDrag();
                }

                SelectedDecloakHelper = name;
            }
            else
            {
                if (name != "Forward")
                {
                    PerfromDrag();
                }
            }
        }

        private GameObject GetDecloakHelperByName(string name)
        {
            GameObject result = null;

            if (name == "Forward")
            {
                result = Selection.ThisShip.GetDecloakHelper().Find(name).gameObject;
            }
            else
            {
                result = MovementTemplates.CurrentTemplate.gameObject;
            }

            return result;
        }

        private string GetNearestDecloakHelper(Vector3 point)
        {
            float minDistance = float.MaxValue;
            KeyValuePair<string, Vector3> nearestDecloakHelper = new KeyValuePair<string, Vector3>();

            foreach (var decloakDirection in AvailableDecloakDirections)
            {
                if (string.IsNullOrEmpty(nearestDecloakHelper.Key))
                {
                    nearestDecloakHelper = decloakDirection;
                    minDistance = Vector3.Distance(point, decloakDirection.Value);
                    continue;
                }
                else
                {
                    float currentDistance = Vector3.Distance(point, decloakDirection.Value);
                    if (currentDistance < minDistance)
                    {
                        nearestDecloakHelper = decloakDirection;
                        minDistance = currentDistance;
                    }
                }
            }

            return nearestDecloakHelper.Key;
        }

        private void PerfromDrag()
        {
            RaycastHit hit;
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(ray, out hit))
            {
                if (ShipStand != null)
                {
                    ShipStand.transform.position = new Vector3(hit.point.x, 0f, hit.point.z);
                    ApplyDecloakRepositionLimits();
                }
            }
        }

        private void ApplyDecloakRepositionLimits()
        {
            Vector3 newPosition = Selection.ThisShip.InverseTransformPoint(ShipStand.transform.position);
            Vector3 fixedPositionRel = newPosition;

            if (newPosition.z > 0.5f)
            {
                fixedPositionRel = new Vector3(fixedPositionRel.x, fixedPositionRel.y, 0.5f);
            }

            if (newPosition.z < -0.5f)
            {
                fixedPositionRel = new Vector3(fixedPositionRel.x, fixedPositionRel.y, -0.5f);
            }

            if (newPosition.x > 0f)
            {
                fixedPositionRel = new Vector3(3, fixedPositionRel.y, fixedPositionRel.z);

                helperDirection = 1f;
                MovementTemplates.CurrentTemplate.eulerAngles = Selection.ThisShip.Model.transform.eulerAngles + new Vector3(0, 180, 0);
            }

            if (newPosition.x < 0f)
            {
                fixedPositionRel = new Vector3(-3, fixedPositionRel.y, fixedPositionRel.z);

                helperDirection = -1f;
                MovementTemplates.CurrentTemplate.eulerAngles = Selection.ThisShip.Model.transform.eulerAngles;
            }

            Vector3 helperPositionRel = Selection.ThisShip.InverseTransformPoint(MovementTemplates.CurrentTemplate.position);
            helperPositionRel = new Vector3(helperDirection * Mathf.Abs(helperPositionRel.x), helperPositionRel.y, helperPositionRel.z);

            if (helperPositionRel.z + 0.25f > fixedPositionRel.z)
            {
                helperPositionRel = new Vector3(helperDirection * Mathf.Abs(helperPositionRel.x), helperPositionRel.y, fixedPositionRel.z - 0.25f);
            }

            if (helperPositionRel.z + 0.75f < fixedPositionRel.z)
            {
                helperPositionRel = new Vector3(helperDirection * Mathf.Abs(helperPositionRel.x), helperPositionRel.y, fixedPositionRel.z - 0.75f);
            }

            Vector3 helperPositionAbs = Selection.ThisShip.TransformPoint(helperPositionRel);
            MovementTemplates.CurrentTemplate.position = helperPositionAbs;

            Vector3 fixedPositionAbs = Selection.ThisShip.TransformPoint(fixedPositionRel);
            ShipStand.transform.position = fixedPositionAbs;
        }

        public override void ProcessClick()
        {
            StopPlanning();
            GameMode.CurrentGameMode.TryConfirmDecloakPosition(ShipStand.transform.position, SelectedDecloakHelper, GetDecloakHelperByName(SelectedDecloakHelper).transform.position, GetDecloakHelperByName(SelectedDecloakHelper).transform.eulerAngles);
        }

        public void StartDecloakExecution(GenericShip ship)
        {
            Pause();

            Selection.ThisShip.ToggleShipStandAndPeg(false);
            DecloakTemplate.SetActive(false);

            DecloakExecutionSubPhase executionSubphase = Phases.StartTemporarySubPhaseNew<DecloakExecutionSubPhase>(
                "Decloak execution",
                CallBack
            );

            executionSubphase.TemporaryShipBase = TemporaryShipBase;
            executionSubphase.HelperDirection = HelperDirection;

            executionSubphase.Start();
        }

        public void CancelDecloak()
        {
            Selection.ThisShip.RemoveAlreadyExecutedAction(typeof(ActionsList.CloakAction));
            Selection.ThisShip.IsLandedOnObstacle = false;
            inReposition = false;
            MonoBehaviour.Destroy(ShipStand);

            GameManagerScript Game = GameObject.Find("GameManager").GetComponent<GameManagerScript>();
            Game.Movement.CollidedWith = null;

            MovementTemplates.HideLastMovementRuler();
            MovementTemplates.CurrentTemplate.gameObject.SetActive(true);

            PreviousSubPhase.Resume();
        }

        private void StopPlanning()
        {
            Roster.SetRaycastTargets(true);
            inReposition = false;
        }

        public void TryConfirmDecloakNetwork(Vector3 shipPosition, string decloakHelper, Vector3 movementTemplatePosition, Vector3 movementTemplateAngles)
        {

            ShipStand.SetActive(true);
            StopPlanning();

            ShipStand.transform.position = shipPosition;

            SelectedDecloakHelper = decloakHelper;

            GetDecloakHelperByName(SelectedDecloakHelper).transform.eulerAngles = movementTemplateAngles;
            GetDecloakHelperByName(SelectedDecloakHelper).transform.position = movementTemplatePosition;

            TryConfirmDecloakPosition();
        }

        public void TryConfirmDecloakPosition()
        {
            DecloakTemplate.SetActive(true);

            obstaclesStayDetectorBase.ReCheckCollisionsStart();
            obstaclesStayDetectorMovementTemplate.ReCheckCollisionsStart();

            GameManagerScript Game = GameObject.Find("GameManager").GetComponent<GameManagerScript>();
            Game.Movement.FuncsToUpdate.Add(UpdateColisionDetection);
        }

        private bool UpdateColisionDetection()
        {
            bool isFinished = false;

            if (updatesCount > 1)
            {
                GetResults();
                isFinished = true;
            }
            else
            {
                updatesCount++;
            }

            return isFinished;
        }

        private void GetResults()
        {
            obstaclesStayDetectorBase.ReCheckCollisionsFinish();
            obstaclesStayDetectorMovementTemplate.ReCheckCollisionsFinish();

            if (IsDecloakAllowed())
            {
                CheckMines();
                Selection.ThisShip.LandedOnObstacles = new List<GenericObstacle>(obstaclesStayDetectorBase.OverlappedAsteroidsNow);
                GameMode.CurrentGameMode.StartDecloakExecution(Selection.ThisShip);
            }
            else
            {
                GameMode.CurrentGameMode.CancelDecloak();
            }
        }

        private void CheckMines()
        {
            foreach (var mineCollider in obstaclesStayDetectorMovementTemplate.OverlapedMinesNow)
            {
                GameObject mineObject = mineCollider.transform.parent.gameObject;
                if (!Selection.ThisShip.MinesHit.Contains(mineObject)) Selection.ThisShip.MinesHit.Add(mineObject);
            }
        }

        private void HidePlanningTemplates()
        {
            Selection.ThisShip.GetDecloakHelper().Find(SelectedDecloakHelper).gameObject.SetActive(false);
        }

        private bool IsDecloakAllowed()
        {
            bool allow = true;

            if (obstaclesStayDetectorBase.OverlapsShipNow)
            {
                Messages.ShowError("Cannot overlap another ship");
                allow = false;
            }
            else if ((!Selection.ThisShip.IsIgnoreObstacles) && (obstaclesStayDetectorBase.OverlapsAsteroidNow || obstaclesStayDetectorMovementTemplate.OverlapsAsteroidNow))
            {
                Messages.ShowError("Cannot overlap asteroid");
                allow = false;
            }
            else if (obstaclesStayDetectorBase.OffTheBoardNow || obstaclesStayDetectorMovementTemplate.OffTheBoardNow)
            {
                Messages.ShowError("Cannot leave the battlefield");
                allow = false;
            }

            return allow;
        }

        public override void Next()
        {
            Phases.CurrentSubPhase = PreviousSubPhase;
            Phases.CurrentSubPhase.Next();
            UpdateHelpInfo();
        }

        public override bool ThisShipCanBeSelected(GenericShip ship, int mouseKeyIsPressed)
        {
            return false;
        }

        public override bool AnotherShipCanBeSelected(GenericShip anotherShip, int mouseKeyIsPressed)
        {
            return false;
        }

        public override void SkipButton()
        {
            StopPlanning();
            CancelDecloak();
            HidePlanningTemplates();
        }

        protected class DecloakDirectionDecisionSubPhase : DecisionSubPhase { }

        protected class DecloakPositionDecisionSubPhase : DecisionSubPhase { }

    }

    public class DecloakExecutionSubPhase : GenericSubPhase
    {
        private float progressCurrent;
        private float progressTarget;

        private float initialRotation;
        private float plannedRotation;

        private bool performingAnimation;

        public GameObject TemporaryShipBase;
        public float HelperDirection;

        public override void Start()
        {
            Name = "Decloak execution";
            IsTemporary = true;
            UpdateHelpInfo();

            StartDecloakExecution();
        }

        private void StartDecloakExecution()
        {
            Rules.Collision.ClearBumps(Selection.ThisShip);

            progressCurrent = 0;
            progressTarget = Vector3.Distance(Selection.ThisShip.GetPosition(), TemporaryShipBase.transform.position);

            initialRotation = (TheShip.GetAngles().y < 180) ? TheShip.GetAngles().y : -(360 - TheShip.GetAngles().y);
            plannedRotation = (TemporaryShipBase.transform.eulerAngles.y - initialRotation < 180) ? TemporaryShipBase.transform.eulerAngles.y : -(360 - TemporaryShipBase.transform.eulerAngles.y);

            Sounds.PlayFly();

            performingAnimation = true;
        }

        public override void Update()
        {
            if (performingAnimation) DoDecloakAnimation();
        }

        private void DoDecloakAnimation()
        {
            float progressStep = 2.5f * Time.deltaTime * Options.AnimationSpeed;
            progressStep = Mathf.Min(progressStep, progressTarget - progressCurrent);
            progressCurrent += progressStep;

            Selection.ThisShip.SetPosition(Vector3.MoveTowards(Selection.ThisShip.GetPosition(), TemporaryShipBase.transform.position, progressStep));

            Selection.ThisShip.RotateModelDuringBarrelRoll(progressCurrent / progressTarget, HelperDirection);
            TheShip.SetRotationHelper2Angles(new Vector3(0, progressCurrent / progressTarget * (plannedRotation - initialRotation), 0));
            Selection.ThisShip.MoveUpwards(progressCurrent / progressTarget);

            if (progressCurrent >= progressTarget)
            {
                performingAnimation = false;
                GameMode.CurrentGameMode.FinishDecloak();
            }
        }

        public void FinishDecloakAnimation()
        {
            performingAnimation = false;

            TheShip.ApplyRotationHelpers();
            TheShip.ResetRotationHelpers();
            TheShip.SetAngles(TemporaryShipBase.transform.eulerAngles);

            MonoBehaviour.Destroy(TemporaryShipBase);

            GameManagerScript Game = GameObject.Find("GameManager").GetComponent<GameManagerScript>();
            Game.Movement.CollidedWith = null;

            MovementTemplates.HideLastMovementRuler();
            MovementTemplates.CurrentTemplate.gameObject.SetActive(true);

            Selection.ThisShip.ToggleShipStandAndPeg(true);
            Selection.ThisShip.FinishPosition(FinishDecloakAnimationPart2);
        }

        private void FinishDecloakAnimationPart2()
        {
            Phases.FinishSubPhase(typeof(DecloakExecutionSubPhase));

            Selection.ThisShip.Tokens.SpendToken(typeof(Tokens.CloakToken), FinishDecloakAnimationPart3);
        }

        private void FinishDecloakAnimationPart3()
        {
            Selection.ThisShip.CallDecloak(CallBack);
        }

        public override void Next()
        {
            Phases.CurrentSubPhase = Phases.CurrentSubPhase.PreviousSubPhase;
            Phases.CurrentSubPhase = Phases.CurrentSubPhase.PreviousSubPhase;
            UpdateHelpInfo();
        }

        public override bool ThisShipCanBeSelected(GenericShip ship, int mouseKeyIsPressed)
        {
            bool result = false;
            return result;
        }

        public override bool AnotherShipCanBeSelected(GenericShip anotherShip, int mouseKeyIsPressed)
        {
            bool result = false;
            return result;
        }

    }

}