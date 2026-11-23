from pathlib import Path
import xml.etree.ElementTree as E
import zipfile,re,math,json

ROOT=Path(__file__).resolve().parents[3]
ns={'m':'http://schemas.openxmlformats.org/spreadsheetml/2006/main'}
with zipfile.ZipFile(ROOT/'Documentation/Economy/Sources/SF_Data_v92.xlsx') as z:
    ss=[''.join(x.itertext()) for x in E.fromstring(z.read('xl/sharedStrings.xml'))] if 'xl/sharedStrings.xml' in z.namelist() else []
    rel={x.attrib['Id']:x.attrib['Target'].lstrip('/') for x in E.fromstring(z.read('xl/_rels/workbook.xml.rels'))}
    target=rel['rId50'];target=target if target.startswith('xl/') else 'xl/'+target
    rows=[]
    for row in E.fromstring(z.read(target)).findall('m:sheetData/m:row',ns):
        d={}
        for c in row:
            v=c.find('m:v',ns);v=v.text if v is not None else ''
            if c.attrib.get('t')=='s':v=ss[int(v)]
            elif c.attrib.get('t')=='inlineStr':v=''.join(c.find('m:is',ns).itertext())
            d[re.sub(r'\d','',c.attrib['r'])]=v
        rows.append(d)
def num(x):return int(float(x))
nodes={};keys={};dep={};gal={};section=''
for d in rows:
    a=d.get('A','')
    if a.startswith('Исследования'):section=a
    if not a.startswith('research_branch_'):continue
    if section=='Исследования':
        uid=num(d['C'].split(' · ')[0]);keys[(a,num(d['B']))]=uid
        nodes[uid]={'name':d['D'],'max':num(d['H']),'start':num(d['J'])}
    elif section=='Исследования / Уровни исследования':
        uid=keys[(a,num(d['B']))];level=num(d['D']);gal[(uid,level)]=num(d['E'])
    elif section=='Исследования / Уровни исследования / Зависимости':
        uid=keys[(a,num(d['B']))];level=num(d['C'])
        dep.setdefault((uid,level),[]).append((num(d['E'].split(' · ')[0]),num(d['F'])))

def run(policy='growth',daily=False,session_minutes=30,rate=1,limit_days=600):
    levels={i:n['start'] for i,n in nodes.items()};jobs={};count=0;milestones={};now=0
    raw_minutes=0;completions=[]
    def capacity():return max(1,(levels[62]+1)//2)
    def fill(t):
        nonlocal raw_minutes
        while len(jobs)<capacity():
            candidates=[]
            for uid,n in nodes.items():
                if uid in jobs or levels[uid]>=n['max']:continue
                nxt=levels[uid]+1
                if any(levels.get(i,0)<l for i,l in dep.get((uid,nxt),[])):continue
                if policy=='no_growth' and uid in (62,63):continue
                duration=10*nxt**3*(1-.05*levels[63])
                duration=math.floor(duration+.5)
                priority=(0 if uid==62 and nxt<=9 else 1 if uid==63 else 2) if policy=='growth' else 0
                candidates.append((priority,duration,uid,nxt))
            if not candidates:return
            _,duration,uid,nxt=min(candidates)
            jobs[uid]={'left':duration,'level':nxt,'start':t,'duration':duration}
            raw_minutes+=duration
    def advance(end,online):
        nonlocal now,count
        while now<end-1e-7:
            if online:fill(now)
            dt=min([end-now]+[j['left']/rate for j in jobs.values()])
            for j in jobs.values():j['left']-=dt*rate
            now+=dt
            done=[i for i,j in jobs.items() if j['left']<1e-6]
            for uid in done:
                j=jobs.pop(uid);levels[uid]=j['level'];count+=1
                completions.append((now,uid,j['level']))
                if uid in (62,63):milestones[str(uid)+'_'+str(j['level'])]=round(now/1440,3)
            if count>=368:return True
        if online:fill(now)
        return count>=368
    sessions=[]
    for day in range(limit_days):
        if not daily and day%6>=4:continue
        for hour in (8,14,20):sessions.append((day*1440+hour*60,day*1440+hour*60+session_minutes))
    first=sessions[0][0];now=first;previous_exit=first
    count_at_105=count_at_150=None;completion_time=None
    for start,end in sessions:
        offline_end=min(start,previous_exit+720)
        if now<offline_end:
            if advance(offline_end,False):completion_time=now;break
        now=start
        if advance(end,True):completion_time=now;break
        previous_exit=end
    return {'policy':policy,'daily':daily,'session_minutes':session_minutes,'science_rate':rate,
        'completion_day_since_first_session':round((completion_time-first)/1440,2) if completion_time else None,
        'completed':count,'capacity_at_end':capacity(),'acceleration_level':levels[63],
        'milestones_days_from_first':{k:round(v-first/1440,2) for k,v in milestones.items()},
        'active_started_minutes_sum':raw_minutes}

results=[run('growth'),run('shortest'),run('no_growth'),run('growth',True,45),run('growth',rate=2)]
print(json.dumps({'node_count':len(nodes),'levels':sum(n['max'] for n in nodes.values()),'start':sum(n['start'] for n in nodes.values()),'galaxy_gates':sorted(set(gal.values())),'results':results},ensure_ascii=False,indent=2))
