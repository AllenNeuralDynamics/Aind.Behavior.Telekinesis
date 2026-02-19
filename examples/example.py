import os

import aind_behavior_telekinesis.task_logic as tl

from .example_w_luts import mock_rig, mock_session


def mock_task_logic() -> tl.AindBehaviorTelekinesisTaskLogic:
    prototype_trial = tl.Action(
        reward_probability=tl.scalar_value(1),
        reward_amount=tl.scalar_value(1),
        reward_delay=tl.scalar_value(0),
        action_duration=tl.scalar_value(1),
        is_operant=False,
        time_to_collect=tl.scalar_value(5),
        lower_action_threshold=tl.scalar_value(0),
        upper_action_threshold=tl.scalar_value(20000),
        continuous_feedback=tl.ManipulatorFeedback(converter_lut_input=[0, 1], converter_lut_output=[1, 10]),
    )
    return tl.AindBehaviorTelekinesisTaskLogic(
        task_parameters=tl.AindTelekinesisTaskParameters(
            rng_seed=None,
            environment=tl.Environment(
                block_statistics=[
                    tl.BlockGenerator(
                        block_size=tl.scalar_value(1000),
                        trial_statistics=tl.Trial(
                            inter_trial_interval=tl.scalar_value(10),
                            quiescence_period=None,
                            response_period=tl.ResponsePeriod(
                                duration=tl.scalar_value(10), has_cue=True, action=prototype_trial
                            ),
                            action_source_0=tl.LoadCellActionSource(channel=0),
                            action_source_1=tl.BehaviorAnalogInputActionSource(channel=0),
                            lut_reference="amazing_lut",
                        ),
                    )
                ],
            ),
            operation_control=tl.OperationControl(
                spout=tl.SpoutOperationControl(
                    default_retracted_position=10, default_extended_position=20, enabled=False
                ),
                action_luts={
                    "amazing_lut": tl.ActionLookUpTableFactory(
                        path="amazing_lut.tiff",
                        offset=0,
                        scale=1,
                        action0_min=0,
                        action0_max=20000,
                        action1_min=0,
                        action1_max=2048,
                    )
                },
            ),
        )
    )


def main(path_seed: str = "./local/{schema}.json"):
    example_session = mock_session()
    example_rig = mock_rig()
    example_task_logic = mock_task_logic()

    os.makedirs(os.path.dirname(path_seed), exist_ok=True)

    models = [example_task_logic, example_session, example_rig]

    for model in models:
        with open(path_seed.format(schema=model.__class__.__name__), "w", encoding="utf-8") as f:
            f.write(model.model_dump_json(indent=2))


if __name__ == "__main__":
    main()
