import logging
from pathlib import Path

from aind_behavior_services.session import AindBehaviorSessionModel
from clabe.apps import (
    AindBehaviorServicesBonsaiApp,
)
from clabe.data_transfer.robocopy import RobocopyService, RobocopySettings
from clabe.launcher import Launcher, LauncherCliArgs
from clabe.pickers import DefaultBehaviorPicker, DefaultBehaviorPickerSettings
from clabe.resource_monitor import ResourceMonitor, available_storage_constraint_factory
from pydantic_settings import CliApp

from aind_behavior_telekinesis import data_contract
from aind_behavior_telekinesis.rig import AindBehaviorTelekinesisRig
from aind_behavior_telekinesis.task_logic import AindBehaviorTelekinesisTaskLogic

logger = logging.getLogger(__name__)


async def experiment(launcher: Launcher) -> None:
    ResourceMonitor(
        constrains=[
            available_storage_constraint_factory(launcher.settings.data_dir, 2e11),
        ]
    ).run()

    # Start experiment setup
    picker = DefaultBehaviorPicker(
        launcher=launcher, settings=DefaultBehaviorPickerSettings(), experimenter_validator=None
    )

    # Pick and register session
    session = picker.pick_session(AindBehaviorSessionModel)
    launcher.register_session(session)

    # Fetch the task settings
    task_logic = picker.pick_task_logic(AindBehaviorTelekinesisTaskLogic)
    # Fetch rig settings
    rig = picker.pick_rig(AindBehaviorTelekinesisRig)

    bonsai_app = AindBehaviorServicesBonsaiApp(
        workflow=Path(r"./src/main.bonsai"),
        temp_directory=launcher.temp_dir,
        rig=rig,
        session=session,
        task_logic=task_logic,
    )
    await bonsai_app.run_async()

    # Run data qc
    if picker.ui_helper.prompt_yes_no_question("Would you like to generate a qc report?"):
        try:
            import webbrowser

            from contraqctor.qc.reporters import HtmlReporter

            from aind_behavior_telekinesis.data_qc.data_qc import make_qc_runner

            vr_dataset = data_contract.dataset(launcher.session_directory)
            runner = make_qc_runner(vr_dataset)
            qc_path = launcher.session_directory / "Behavior" / "Logs" / "qc_report.html"
            reporter = HtmlReporter(output_path=qc_path)
            runner.run_all_with_progress(reporter=reporter)
            webbrowser.open(qc_path.as_uri(), new=2)
        except Exception as e:
            logger.error(f"Failed to run data QC: {e}")

        # Transfer data
        is_transfer = picker.ui_helper.prompt_yes_no_question("Would you like to transfer data?")
        if not is_transfer:
            logger.info("Data transfer skipped by user.")
            return

        launcher.copy_logs()
        RobocopyService(source=launcher.session_directory, settings=RobocopySettings()).transfer()
        return


class ClabeCli(LauncherCliArgs):
    def cli_cmd(self):
        launcher = Launcher(settings=self)
        launcher.run_experiment(experiment)
        return None


def main() -> None:
    CliApp().run(ClabeCli)


if __name__ == "__main__":
    main()
