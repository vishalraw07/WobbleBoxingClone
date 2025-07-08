mergeInto(LibraryManager.library, {
  SendMatchResult: function(matchIdPtr, playerIdPtr, opponentIdPtr ,outcomePtr, score, Opponentscore) {
     var outcome = UTF8ToString(outcomePtr);
    var matchId = UTF8ToString(matchIdPtr);
    var playerId = UTF8ToString(playerIdPtr);
    var opponentId = UTF8ToString(opponentIdPtr);
    parent.postMessage({
      type: 'match_result',
      payload: {
        matchId: matchId ,
        playerId: playerId,
        opponentId: opponentId,
        outcome: outcome,
        score: score,
        Opponentscore: Opponentscore
      }
    }, '*');
  },

  SendMatchAbort: function(messagePtr, errorPtr, errorCodePtr) {
    var message = UTF8ToString(messagePtr);
    var error = UTF8ToString(errorPtr);
    var errorCode = UTF8ToString(errorCodePtr);

    parent.postMessage({
      type: 'match_abort',
      payload: {
        message: message,
        error: error,
        errorCode: errorCode
      }
    }, '*');
  }

});