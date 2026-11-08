from pathlib import Path
p=Path('Assets/SealCombat/Runtime/Effects/SkillEffects.cs')
s=p.read_text(encoding='utf-8-sig')
start=s.index('        private void UpdateHeld(double now)')
end=s.index('        private void UpdateHeldPosition(Pose wrist)',start)
s=s[:start]+'''        private void UpdateHeld(double now)
        {
            if (now >= heldExpiresAt) { ObserveThrow("expired","火球持握超时",default,default,0,false); ClearHeld(); Report("火球持握超时，已消散"); return; }
            if (!head || !TryGetWrist(heldInLeftHand, out Pose wrist, out double observed))
            {
                throwInputValid = false; releasePoseValid = false;
                heldObject.SetActive(false);
                if (lostAt < 0) { lostAt = now; ResetMotion(now); Report("手部追踪丢失，火球已隐藏"); }
                ObserveThrow("tracking_lost","持球手追踪缺失或过期",default,default,0,false);
                if (now - lostAt >= heldParameters.LossGrace) { ClearHeld(); Report("手部追踪未恢复，火球已消散"); }
                return;
            }
            if (lostAt >= 0) { lostAt = -1; ResetMotion(now); Report("手部追踪恢复，请重新握住再投掷"); }
            heldObject.SetActive(true);
            UpdateHeldPosition(wrist);
            if (PushInputSuppressed || (throwState != null && ThrowInputSuppressed))
            {
                ResetMotion(now);
                ObserveThrow("suppressed","界面、角色状态或其他输入仲裁阻止投掷",wrist,default,observed,false);
                return;
            }
            if (throwState == null) { TrackPush(wrist.position,observed,now); return; }
            ThrowGripSample grip = ThrowGripProvider != null ? ThrowGripProvider(heldInLeftHand,heldParameters.Throwing) : default;
            if (now < armAt) { ObserveThrow("warming_up","生成或恢复追踪后的准备时间",wrist,grip,observed,true); return; }
            if (!double.IsFinite(grip.observed) || Math.Abs(grip.observed-observed) > .01)
            {
                ResetMotion(now);
                ObserveThrow("sample_skew","手形与腕骨不在同一观测帧",wrist,grip,observed,true); return;
            }
            double handNow=HandNow;
            Vector3 velocity=throwState.Process(wrist.position,head.position,grip,handNow);
            AssistedThrowCandidate candidate=throwIntent?.Process(wrist.position,head.position,head.forward,grip,handNow) ?? default;
            bool backwards = throwIntent != null && throwIntent.ContradictsWindup(throwState.Estimate.velocity);
            if (velocity.sqrMagnitude>.0001f && throwIntent != null && throwIntent.ContradictsWindup(velocity)) velocity=Vector3.zero;
            bool pending=throwState.ReleasePending && !backwards;
            Vector3 currentCenter=heldObject.transform.position;
            throwInputValid=(grip.fistValid||grip.pinchValid) && observed<=handNow && handNow-observed<=heldParameters.Throwing.sampleMaxAge;
            throwBlockReason=null;
            Physics.SyncTransforms();
            // A mature physical release wins. An unfinished release must not starve assisted actions.
            bool assisted = velocity.sqrMagnitude<=.0001f && candidate.kind!=AssistedThrowKind.None;
            string mode=assisted?candidate.kind.ToString():"GripRelease";
            if (assisted)
            {
                if (!ThrowPathClear(hasSafeHeldPoint?safeHeldPoint:currentCenter,currentCenter))
                { RejectThrow("辅助发射路径或出手点被阻挡",wrist,grip,observed); return; }
                releasePose=new Pose(currentCenter,wrist.rotation); releasePoseValid=true;
                velocity=candidate.direction * heldParameters.Speed;
            }
            else if (pending && throwState.Estimate.accepted && (!releasePoseValid || releasePoseCandidate!=throwState.CandidateId))
            {
                if (!ThrowPathClear(hasSafeHeldPoint?safeHeldPoint:currentCenter,currentCenter))
                { RejectThrow("释放位置或甩手路径被阻挡，请重新握住",wrist,grip,observed); return; }
                releasePose=new Pose(currentCenter,wrist.rotation); releasePoseValid=true; releasePoseCandidate=throwState.CandidateId;
            }
            if (velocity.sqrMagnitude>.0001f)
            {
                if (!releasePoseValid || !ThrowPathClear(releasePose.position,releasePose.position))
                { RejectThrow("释放点被障碍占据，未发射",wrist,grip,observed); return; }
                heldObject.transform.SetPositionAndRotation(releasePose.position,releasePose.rotation);
                ObserveThrow("launched",assisted?(candidate.kind==AssistedThrowKind.ReachHold?"伸手保持发射":"后拉前送评分发射"):"握持松手投掷发射",wrist,grip,observed,true,velocity,mode);
                Launch(velocity.normalized); return;
            }
            if (pending && releasePoseValid) heldObject.transform.SetPositionAndRotation(releasePose.position,releasePose.rotation);
            else
            {
                releasePoseValid=false;
                if (!ThrowPathClear(hasSafeHeldPoint?safeHeldPoint:currentCenter,currentCenter))
                { RejectThrow("持球位置或移动路径被阻挡，请移回可用空间",wrist,grip,observed); return; }
                safeHeldPoint=currentCenter; hasSafeHeldPoint=true;
            }
            string reason=throwIntent != null ? throwIntent.Score.reason+"；松手："+throwState.Status : throwState.Status;
            if (backwards) reason="后拉阶段：不采用朝后的松手轨迹；"+reason;
            ObserveThrow("frame",reason,wrist,grip,observed,true);
        }

        private void RejectThrow(string reason,Pose wrist,ThrowGripSample grip,double observed)
        {
            // Preserve the candidate and score before resetting. No other mode retries this action.
            ObserveThrow("rejected",reason,wrist,grip,observed,true);
            ResetMotion(Now); throwBlockReason=reason;
        }

        private void ObserveThrow(string kind,string reason,Pose wrist,ThrowGripSample grip,double observed,bool hasSample,Vector3 velocity=default,string mode="")
        {
            if (throwState==null) return;
            var observation=new HandThrowObservation
            {
                schemaVersion=2,shotId=shotId,sequence=++observationSequence,eventType=kind,releaseMode=mode,
                source=WristPoseOverride!=null?"测试注入":"骨骼快照/"+(lab?lab.name:"无采样器"),
                hasSample=hasSample,suppressed=ThrowInputSuppressed||PushInputSuppressed,
                observed=hasSample&&double.IsFinite(grip.observed)?grip.observed:0,wristObserved=double.IsFinite(observed)?observed:0,
                now=HandNow,hand=hasSample?wrist.position:Vector3.zero,head=head?head.position:Vector3.zero,headForward=head?head.forward:Vector3.zero,
                grip=grip,score=CurrentThrowScore,estimate=throwState.PreviewEstimate.samples>0&&!throwState.ReleasePending?throwState.PreviewEstimate:throwState.Estimate,
                status=reason,armed=throwState.Armed||(throwIntent?.Armed??false),launchVelocity=velocity,candidateId=throwState.CandidateId,
                pending=throwState.ReleasePending,inputValid=throwInputValid,gripProgress=throwState.GripProgress,releaseProgress=throwState.ReleaseProgress,
                releaseOrigin=releasePoseValid?releasePose.position:HeldThrowPosition
            };
            LastThrowObservation=observation; HasThrowObservation=true;
            if (kind=="launched"||kind=="rejected"||kind=="expired"||kind=="tracking_lost"||kind=="sample_skew")
                LastThrowOutcome=reason+"；动作分 "+(observation.score.available?$"{observation.score.total:0.0}/{observation.score.threshold:0}":"未计算");
            if (ThrowObserved!=null) foreach(Action<HandThrowObservation> listener in ThrowObserved.GetInvocationList())
                try { listener(observation); } catch(Exception error) { Debug.LogWarning("投掷诊断监听失败："+error.Message,this); }
        }

''' + s[end:]
p.write_text(s,encoding='utf-8')
