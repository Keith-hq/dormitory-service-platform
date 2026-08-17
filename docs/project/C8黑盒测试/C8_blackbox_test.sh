#!/usr/bin/env bash
# C8 安全社区黑盒测试（王浩宇）—— 访客二维码 + 房间投票一人一票
# 前置：后端 http://localhost:5000 已启动；种子数据 database/seed/c8_blackbox_seed.sql 已灌入。
# 用法：bash C8_blackbox_test.sh
set -u
BASE="http://localhost:5000"
PASS=0; FAIL=0

# ---------- 工具函数 ----------
say()  { printf '\n==== %s ====\n' "$1"; }
req()  { # req <label> <method> <path> <token> [json]
  local label="$1" method="$2" path="$3" tok="$4" body="${5:-}"
  local resp code
  if [ -n "$body" ]; then
    resp=$(curl -s -w '\n__HTTP__%{http_code}' -X "$method" "$BASE$path" \
      -H "Content-Type: application/json" -H "Authorization: Bearer $tok" -d "$body")
  else
    resp=$(curl -s -w '\n__HTTP__%{http_code}' -X "$method" "$BASE$path" \
      -H "Authorization: Bearer $tok")
  fi
  code=$(printf '%s' "$resp" | grep -o '__HTTP__[0-9]*' | tail -1 | cut -d'_' -f3)
  body2=$(printf '%s' "$resp" | sed 's/__HTTP__[0-9]*$//')
  # 基础退出码校验：HTTP 5xx 视为服务端异常，计入失败
  case "$code" in
    5*) FAIL=$((FAIL+1)) ;;
    *) PASS=$((PASS+1)) ;;
  esac
  printf '[%s] %s %s -> HTTP %s\n%s\n' "$label" "$method" "$path" "$code" "$body2"
}
login() { # login <loginName> <password> -> token
  curl -s -X POST "$BASE/api/auth/login" -H "Content-Type: application/json" \
    -d "{\"loginName\":\"$1\",\"password\":\"$2\"}" \
    | python -c "import sys,json;d=json.load(sys.stdin);print(d['data']['token'] if d.get('code')==200 else '')"
}
jget() { python -c "import sys,json;d=json.load(sys.stdin);print($1)"; }

T1=$(login IT_STU_001 Test1234)
T2=$(login IT_STU_002 Test1234)
T3=$(login IT_STU_003 Test1234)
echo "登录令牌获取: T1=$(test -n "$T1" && echo ok || echo FAIL) T2=$(test -n "$T2" && echo ok || echo FAIL) T3=$(test -n "$T3" && echo ok || echo FAIL)"

# ============================================================
say "C8-001 访客二维码：申请 -> 查询 -> 撤销"
# ============================================================
# 1) 申请（endTime 用未来时间）
END=$(python -c "import datetime;print((datetime.datetime.now()+datetime.timedelta(days=1)).strftime('%Y-%m-%dT%H:%M:%S'))")
req "C8-001-1 申请访客授权" POST "/api/visitor-authorizations" "$T1" \
  "{\"visitorName\":\"访客甲\",\"visitReason\":\"探访\",\"endTime\":\"$END\"}"

# 2) 查询我的授权列表
req "C8-001-2 查询我的授权" GET "/api/students/IT_STU_001/visitor-authorizations" "$T1"

# 3) 取第一条 authId 查看凭证
AUTH_ID=$(curl -s "$BASE/api/students/IT_STU_001/visitor-authorizations" -H "Authorization: Bearer $T1" \
  | python -c "import sys,json;d=json.load(sys.stdin);items=d['data']['items'];print(items[0]['authorizationId'] if items else '')")
echo "  authId = $AUTH_ID"
req "C8-001-3 查看授权凭证" GET "/api/visitor-authorizations/$AUTH_ID" "$T1"

# 4) 撤销
req "C8-001-4 撤销授权" POST "/api/visitor-authorizations/$AUTH_ID/revoke" "$T1"

# 5) 重复撤销（应报错）
req "C8-001-5 重复撤销(负例)" POST "/api/visitor-authorizations/$AUTH_ID/revoke" "$T1"

# 6) 截止时间过去(负例)
req "C8-001-6 截止时间过去(负例)" POST "/api/visitor-authorizations" "$T1" \
  "{\"visitorName\":\"访客乙\",\"endTime\":\"2020-01-01T00:00:00\"}"

# 7) 越权查看他人凭证(负例)：T1 试图查看 T2 的授权（先让 T2 申请一条）
req "C8-001-7 T2 申请(供越权测试)" POST "/api/visitor-authorizations" "$T2" \
  "{\"visitorName\":\"访客丙\",\"endTime\":\"$END\"}"
AUTH2=$(curl -s "$BASE/api/students/IT_STU_002/visitor-authorizations" -H "Authorization: Bearer $T2" \
  | python -c "import sys,json;d=json.load(sys.stdin);items=d['data']['items'];print(items[0]['authorizationId'] if items else '')")
req "C8-001-8 越权查看他人凭证(负例)" GET "/api/visitor-authorizations/$AUTH2" "$T1"

# ============================================================
say "C8-002 房间投票一人一票"
# ============================================================
# 1) 查询房间投票列表（空）
req "C8-002-1 查询房间投票" GET "/api/rooms/900101/votes" "$T1"

# 2) T1 发起投票
END2=$(python -c "import datetime;print((datetime.datetime.now()+datetime.timedelta(days=3)).strftime('%Y-%m-%dT%H:%M:%S'))")
resp=$(curl -s -X POST "$BASE/api/room-votes" -H "Content-Type: application/json" -H "Authorization: Bearer $T1" \
  -d "{\"roomId\":900101,\"topic\":\"是否安装空调\",\"deadline\":\"$END2\",\"eligibleCount\":4}")
echo "[C8-002-2 发起投票] POST /api/room-votes -> $resp"
VOTE_ID=$(printf '%s' "$resp" | python -c "import sys,json;d=json.load(sys.stdin);print(d['data']['voteId'] if d.get('data') and 'voteId' in d['data'] else '')")
echo "  voteId = $VOTE_ID"

# 3) T1 投票 同意
req "C8-002-3 T1投同意" POST "/api/room-votes/$VOTE_ID/responses" "$T1" '{"choice":"同意"}'

# 4) T1 重复投票（负例，应报错）
req "C8-002-4 T1重复投(负例)" POST "/api/room-votes/$VOTE_ID/responses" "$T1" '{"choice":"不同意"}'

# 5) T2 投票 不同意
req "C8-002-5 T2投不同意" POST "/api/room-votes/$VOTE_ID/responses" "$T2" '{"choice":"不同意"}'

# 6) 统计
req "C8-002-6 查看统计" GET "/api/room-votes/$VOTE_ID" "$T1"

# 7) 非本房间学生投票（负例，应被拒绝）
req "C8-002-7 非本房间学生投(负例)" POST "/api/room-votes/$VOTE_ID/responses" "$T3" '{"choice":"同意"}'

# 8) 非法选项（负例）
req "C8-002-8 非法选项(负例)" POST "/api/room-votes/$VOTE_ID/responses" "$T1" '{"choice":"弃权"}'

# 9) 不存在的房间发起投票（负例）
req "C8-002-9 不存在房间(负例)" POST "/api/room-votes" "$T1" \
  "{\"roomId\":999999,\"topic\":\"x\",\"eligibleCount\":4}"

echo ""
echo "================ 测试结束 ================"
echo "PASS=$PASS FAIL=$FAIL"
# 退出码：有 5xx 服务端异常则返回 1，便于 CI 判定
[ "$FAIL" -eq 0 ] && exit 0 || exit 1
