from __future__ import annotations

from enum import Enum
from typing import Annotated, Dict, List, Literal, Optional, Self, Union

import aind_behavior_services.task_logic.distributions as distributions
from aind_behavior_services.calibration.load_cells import LoadCellChannel
from aind_behavior_services.task_logic import AindBehaviorTaskLogicModel, TaskParameters
from pydantic import BaseModel, Field, model_validator
from typing_extensions import TypeAliasType

__version__ = "0.1.0"


def scalar_value(value: float) -> distributions.Scalar:
    """
    Helper function to create a scalar value distribution for a given value.

    Args:
        value (float): The value of the scalar distribution.

    Returns:
        distributions.Scalar: The scalar distribution type.
    """
    return distributions.Scalar(distribution_parameters=distributions.ScalarDistributionParameter(value=value))


def uniform_distribution_value(min: float, max: float) -> distributions.UniformDistribution:
    """
    Helper function to create a uniform distribution for a given range.

    Args:
        min (float): The minimum value of the uniform distribution.
        max (float): The maximum value of the uniform distribution.

    Returns:
        distributions.Uniform: The uniform distribution type.
    """
    return distributions.UniformDistribution(
        distribution_parameters=distributions.UniformDistributionParameters(max=max, min=min)
    )


def normal_distribution_value(mean: float, std: float) -> distributions.Normal:
    """
    Helper function to create a normal distribution for a given range.

    Args:
        mean (float): The mean value of the normal distribution.
        std (float): The standard deviation of the normal distribution.

    Returns:
        distributions.Normal: The normal distribution type.
    """
    return distributions.NormalDistribution(
        distribution_parameters=distributions.NormalDistributionParameters(mean=mean, std=std)
    )


class ContinuousFeedbackMode(str, Enum):
    """Defines the feedback mode"""

    NONE = "None"
    AUDIO = "Audio"
    MANIPULATOR = "Manipulator"


class _ContinuousFeedbackBase(BaseModel):
    continuous_feedback_mode: ContinuousFeedbackMode = Field(
        default=ContinuousFeedbackMode.NONE, description="Continuous feedback mode"
    )
    converter_lut_input: List[Annotated[float, Field(ge=0, le=1)]] = Field(
        default=[0, 1], min_length=2, description="Normalized input domain. All values should be between 0 and 1"
    )
    converter_lut_output: List[float] = Field(
        default=[0, 1],
        min_length=2,
        description="Output domain used to linearly interpolate the input values to the output values",
    )

    @model_validator(mode="after")
    def _validate_lut(self) -> Self:
        if len(self.converter_lut_input) != len(self.converter_lut_output):
            raise ValueError("Input and output LUT must have the same length")
        return self


class ManipulatorFeedback(_ContinuousFeedbackBase):
    continuous_feedback_mode: Literal[ContinuousFeedbackMode.MANIPULATOR] = ContinuousFeedbackMode.MANIPULATOR


class AudioFeedback(_ContinuousFeedbackBase):
    continuous_feedback_mode: Literal[ContinuousFeedbackMode.AUDIO] = ContinuousFeedbackMode.AUDIO


ContinuousFeedback = TypeAliasType(
    "ContinuousFeedback",
    Annotated[Union[ManipulatorFeedback, AudioFeedback], Field(discriminator="continuous_feedback_mode")],
)


class Action(BaseModel):
    """Defines an abstract class for an harvest action"""

    reward_probability: distributions.Distribution = Field(
        default=scalar_value(1), description="Probability of reward", validate_default=True
    )
    reward_amount: distributions.Distribution = Field(
        default=scalar_value(1), description="Amount of reward to be delivered", validate_default=True
    )
    reward_delay: distributions.Distribution = Field(
        default=scalar_value(1),
        description="Delay between successful harvest and reward delivery",
        validate_default=True,
    )
    action_duration: distributions.Distribution = Field(
        default=scalar_value(0.5), description="Duration that the action much stay above threshold", validate_default=True
    )
    upper_action_threshold: distributions.Distribution = Field(
        default=scalar_value(20000),
        description="Upper bound of the cached action required to get reward.",
        validate_default=True,
    )
    lower_action_threshold: distributions.Distribution = Field(
        default=scalar_value(0),
        description="Lower bound of the cached action required to get reward. This determines from which value the action is accumulated from.",
        validate_default=True,
    )
    is_operant: bool = Field(default=True, description="Whether the reward delivery is contingent on licking.")
    time_to_collect: Optional[distributions.Distribution] = Field(
        default=None,
        description="Time to collect the reward after it is available. If null, the reward will be available indefinitely.",
        validate_default=True,
    )
    continuous_feedback: Optional[ContinuousFeedback] = Field(default=None, description="Continuous feedback settings")


class QuiescencePeriod(BaseModel):
    """Defines a quiescence settings"""

    duration: distributions.Distribution = Field(
        default=scalar_value(0.5), description="Duration of the quiescence period", validate_default=True
    )
    action_threshold: float = Field(default=0, description="Time out for the quiescence period")
    has_cue: bool = Field(default=False, description="Whether to use a cue to signal the start of the period.")


class ResponsePeriod(BaseModel):
    """Defines a response period"""

    duration: distributions.Distribution = Field(
        default=scalar_value(0.5),
        description="Duration of the response period. I.e. the time the animal has to make a choice.",
        validate_default=True,
    )
    has_cue: bool = Field(default=True, description="Whether to use a cue to signal the start of the period.")
    action: Action = Field(
        default=Action(), description="Action to be performed during the response period", validate_default=True
    )


class ActionSourceType(str, Enum):
    """Defines the source of the data to use in the action"""

    LOADCELL = "LoadCell"
    BEHAVIOR_ANALOG_INPUT = "BehaviorAnalogInput"


class _ActionSource(BaseModel):
    action_source: ActionSourceType = Field(
        default=ActionSourceType.LOADCELL, description="Source of the data to use in the action"
    )


class LoadCellActionSource(_ActionSource):
    action_source: Literal[ActionSourceType.LOADCELL] = ActionSourceType.LOADCELL
    channel: LoadCellChannel = Field(default=0, description="Index of the load cell channel to use")


class BehaviorAnalogInputActionSource(_ActionSource):
    action_source: Literal[ActionSourceType.BEHAVIOR_ANALOG_INPUT] = ActionSourceType.BEHAVIOR_ANALOG_INPUT
    channel: Literal[0, 1] = Field(default=0, description="Index of the behavior analog input channel to use")


ActionSource = TypeAliasType(
    "ActionSource",
    Annotated[Union[LoadCellActionSource, BehaviorAnalogInputActionSource], Field(discriminator="action_source")],
)


class Trial(BaseModel):
    """Defines a trial"""

    inter_trial_interval: distributions.Distribution = Field(
        default=scalar_value(0.5), description="Time between trials", validate_default=True
    )
    quiescence_period: Optional[QuiescencePeriod] = Field(default=None, description="Quiescence settings")
    response_period: ResponsePeriod = Field(
        default=ResponsePeriod(), validate_default=True, description="Response settings"
    )
    action_source_0: ActionSource = Field(..., description="Action source for the first axis to be sample from the LUT")
    action_source_1: Optional[ActionSource] = Field(
        default=None,
        description="Action source for the second axis to be sample from the LUT. If None, LUT will be sampled from [action_source_0, 0]",
    )
    lut_reference: str = Field(
        ..., description="Reference to the look up table image. Should be a key in the action_luts dictionary"
    )


class BlockStatisticsMode(str, Enum):
    """Defines the mode of the environment"""

    BLOCK = "Block"
    BLOCK_GENERATOR = "BlockGenerator"


class Block(BaseModel):
    mode: Literal[BlockStatisticsMode.BLOCK] = BlockStatisticsMode.BLOCK
    trials: List[Trial] = Field(default=[], description="List of trials in the block")
    shuffle: bool = Field(default=False, description="Whether to shuffle the trials in the block")
    repeat_count: Optional[int] = Field(
        default=0, description="Number of times to repeat the block. If null, the block will be repeated indefinitely"
    )


class BlockGenerator(BaseModel):
    mode: Literal[BlockStatisticsMode.BLOCK_GENERATOR] = BlockStatisticsMode.BLOCK_GENERATOR
    block_size: distributions.Distribution = Field(
        default=uniform_distribution_value(min=50, max=60), validate_default=True, description="Size of the block"
    )
    trial_statistics: Trial = Field(..., description="Statistics of the trials in the block")


BlockStatistics = TypeAliasType("BlockStatistics", Annotated[Union[Block, BlockGenerator], Field(discriminator="mode")])


class Environment(BaseModel):
    block_statistics: List[BlockStatistics] = Field(..., description="Statistics of the environment")
    shuffle: bool = Field(default=False, description="Whether to shuffle the blocks")
    repeat_count: Optional[int] = Field(
        default=0,
        description="Number of times to repeat the environment. If null, the environment will be repeated indefinitely",
    )


class ActionLookUpTable(BaseModel):
    path: str = Field(
        ..., description="Reference to the look up table image. Should be a 1 channel image. Value = LUT[Left, Right]"
    )

    offset: float = Field(default=0, description="Offset to add to the look up table value")

    scale: float = Field(default=1, description="Scale to multiply the look up table value")

    left_min: float = Field(
        ..., description="The lower value of Left force used to linearly scale the input coordinate to."
    )
    left_max: float = Field(
        ..., description="The upper value of Left force used to linearly scale the input coordinate to."
    )
    right_min: float = Field(
        ..., description="The lower value of Right force used to linearly scale the input coordinate to."
    )
    right_max: float = Field(
        ..., description="The upper value of Right force used to linearly scale the input coordinate to."
    )

    @model_validator(mode="after")
    def _validate_bounds(self) -> Self:
        if self.left_min > self.left_max:
            raise ValueError("Left min must be less than left max")
        if self.right_min > self.right_max:
            raise ValueError("Right min must be less than right max")
        return self


class SpoutOperationControl(BaseModel):
    default_retracted_position: float = Field(default=0, description="Default retracted position (mm)")
    default_extended_position: float = Field(default=0, description="Default extended position (mm)")
    enabled: bool = Field(default=True, description="Whether the spout control is enabled")


class OperationControl(BaseModel):
    action_luts: Dict[str, ActionLookUpTable] = Field(
        ..., validate_default=True, description="Look up tables to derive action output from."
    )
    spout: SpoutOperationControl = Field(
        default=SpoutOperationControl(), validate_default=True, description="Operation control for spout"
    )


class AindTelekinesisTaskParameters(TaskParameters):
    environment: Environment = Field(..., description="Environment settings")
    operation_control: OperationControl = Field(default=..., validate_default=True, description="Operation control")

    @model_validator(mode="after")
    def _check_valid_lut_reference(self) -> Self:
        action_luts = self.operation_control.action_luts  # pylint: disable=no-member
        for block in self.environment.block_statistics:  # pylint: disable=no-member
            if isinstance(block, BlockGenerator):
                if block.trial_statistics.lut_reference not in action_luts:
                    raise ValueError(
                        f"Look up table reference '{block.trial_statistics.lut_reference}' not found in action_luts"
                    )
            elif isinstance(block, Block):
                for trial in block.trials:
                    if trial.lut_reference not in action_luts:
                        raise ValueError(f"Look up table reference '{trial.lut_reference}' not found in action_luts")
            else:  # guard clause
                raise ValueError(f"Block statistics mode '{block.mode}' not recognized")
        return self


class AindTelekinesisTaskLogic(AindBehaviorTaskLogicModel):
    version: Literal[__version__] = __version__
    name: str = Field(default="AindTelekinesis", description="Name of the task logic", frozen=True)
    task_parameters: AindTelekinesisTaskParameters = Field(..., description="Parameters of the task logic")
